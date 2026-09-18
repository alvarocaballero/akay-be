using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using CsvHelper;
using CsvHelper.Configuration;
using FluentValidation;

namespace Akay.Be.Application.Features.Students;

public sealed record ImportStudentsCsvCommand([property: JsonIgnore] int CenterId,
                                              int CourseId,
                                              [property: JsonIgnore] string? CsvContent) : ICommand<ImportStudentsCsvResponse>;

public sealed record ImportStudentsCsvResponse(int ImportedCount,
                                               int UpdatedCount,
                                               int SkippedCount,
                                               int FailedCount,
                                               IReadOnlyList<StudentCsvImportError> Errors);

public sealed record StudentCsvImportError(int Row, string? Email, string Reason);

internal sealed record StudentCsvRow(int RowNumber,
                                     string? Email,
                                     string? StudentNumber,
                                     string? FirstName,
                                     string? LastName);

internal sealed partial class ImportStudentsCsvCommandHandler(IAdminScopeService adminScope,
                                                              IPersistenceResultExecutor persistence,
                                                              IStudentRepository studentRepository,
                                                              IUserRepository userRepository,
                                                              ICourseRepository courseRepository) : ICommandHandler<ImportStudentsCsvCommand, ImportStudentsCsvResponse>
{
    public const int MaxImportRows = 2000;

    private const int MaxEmailLength = 256;
    private const int MaxNameLength = 100;
    private const int MaxStudentNumberLength = 50;

    private static readonly Error EmailAlreadyExists = Error.Conflict("user.email_exists", "Ya existe un usuario con alguno de los emails del archivo.");

    // SQL Server no expone ConstraintName: el executor cae en las claves genéricas por Number (sql.*).
    // Npgsql y proveedores que sí exponen ConstraintName usan el nombre literal del índice.
    private static readonly IReadOnlyDictionary<string, Error> KnownPersistenceErrors = new Dictionary<string, Error>
    {
        ["IX_User_Email"] = EmailAlreadyExists,
        ["sql.unique_violation"] = EmailAlreadyExists,
        ["sql.not_null_violation"] = Error.Validation("csv.invalid_row", "Alguna fila del archivo deja un campo obligatorio sin valor."),
        ["sql.foreign_key_violation"] = Error.Conflict("course.state_changed", "El curso o sus asignaturas han cambiado mientras se importaba el archivo. Vuelva a intentarlo."),
    };

    public async ValueTask<Result<ImportStudentsCsvResponse>> Handle(ImportStudentsCsvCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var centerCheck = await adminScope.EnsureAdminOfCenterAsync(request.CenterId, cancellationToken);
        if (centerCheck.IsFailure)
            return centerCheck.Error;

        var courseAccess = await adminScope.EnsureCanWriteCourseAsync(request.CourseId, cancellationToken);
        if (courseAccess.IsFailure)
            return courseAccess.Error;

        var course = await courseRepository.GetWithFullGraphAsync(request.CourseId, cancellationToken: cancellationToken);
        if (course is null)
            return Error.NotFound("course.not_found", $"Curso {request.CourseId} no encontrado.");

        if (course.AcademicPeriod.CenterId != request.CenterId)
            return Error.Forbidden("course.student_wrong_center", "Los estudiantes deben pertenecer al mismo centro que el curso.");

        var parseResult = ParseCsv(request.CsvContent, cancellationToken);
        if (parseResult.IsFailure)
            return parseResult.Error;

        var rows = parseResult.Value!;
        var usersByEmail = (await userRepository.GetByEmailsAsync(rows
                                                                      .Where(row => row.Email is not null)
                                                                      .Select(row => row.Email!),
                                                                  cancellationToken))
            .ToDictionary(user => user.Email, StringComparer.OrdinalIgnoreCase);

        var studentsByUserId = (await studentRepository.GetByUserIdsForCenterAsync(usersByEmail.Values.Select(user => user.Id),
                                                                                   request.CenterId,
                                                                                   cancellationToken))
            .ToDictionary(student => student.UserId);

        var errors = new List<StudentCsvImportError>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var importedCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var failureReason = GetRowFailureReason(row, seenEmails);
            if (failureReason is not null)
            {
                errors.Add(new StudentCsvImportError(row.RowNumber, row.Email, failureReason));
                continue;
            }

            if (usersByEmail.TryGetValue(row.Email!, out var existingUser))
            {
                if (UpdateExistingStudent(row, existingUser, studentsByUserId, request.CenterId, course))
                    updatedCount++;
                else
                    skippedCount++;
            }
            else
            {
                ImportRow(row, request.CenterId, course);
                importedCount++;
            }
        }

        if (importedCount == 0 && updatedCount == 0 && skippedCount == 0)
            return Error.Validation("csv.no_valid_rows", "El archivo no contiene ninguna fila valida para importar.");

        var save = await persistence.TrySaveChangesAsync(KnownPersistenceErrors, cancellationToken);
        if (save.IsFailure)
            return save.Error;

        return new ImportStudentsCsvResponse(importedCount, updatedCount, skippedCount, errors.Count, errors);
    }

    private bool UpdateExistingStudent(StudentCsvRow row,
                                       User existingUser,
                                       Dictionary<int, Student> studentsByUserId,
                                       int centerId,
                                       Course course)
    {
        var changed = false;

        if (!string.Equals(existingUser.FirstName, row.FirstName, StringComparison.Ordinal)
            || !string.Equals(existingUser.LastName, row.LastName, StringComparison.Ordinal))
        {
            // Se preserva el email almacenado: el del CSV solo coincide case-insensitive y
            // cambiar el casing dispararia la limpieza del ExternalId en UpdateProfile.
            existingUser.UpdateProfile(existingUser.Email, row.FirstName!, row.LastName!);
            changed = true;
        }

        if (!studentsByUserId.TryGetValue(existingUser.Id, out var student))
        {
            if (!existingUser.RoleAssignments.Any(assignment => assignment.CenterId == centerId && assignment.Role == UserRole.Student))
                existingUser.AssignRole(centerId, UserRole.Student);

            student = Student.Create(existingUser, centerId, row.StudentNumber);
            studentRepository.Add(student);
            studentsByUserId[existingUser.Id] = student;
            course.EnrollStudent(student, null);
            return true;
        }

        if (row.StudentNumber is not null && student.StudentNumber != row.StudentNumber)
        {
            student.ChangeStudentNumber(row.StudentNumber);
            changed = true;
        }

        changed |= course.EnrollStudent(student, null).Changed;
        return changed;
    }

    private static string? GetRowFailureReason(StudentCsvRow row, HashSet<string> seenEmails)
    {
        if (row.Email is null || row.FirstName is null || row.LastName is null)
            return "Email, FirstName y LastName son requeridos.";

        if (!StudentEmailRegex().IsMatch(row.Email))
            return "El email no tiene un formato valido.";

        if (row.Email.Length > MaxEmailLength)
            return $"El email no puede superar {MaxEmailLength} caracteres.";

        if (row.FirstName.Length > MaxNameLength)
            return $"FirstName no puede superar {MaxNameLength} caracteres.";

        if (row.LastName.Length > MaxNameLength)
            return $"LastName no puede superar {MaxNameLength} caracteres.";

        if (row.StudentNumber?.Length > MaxStudentNumberLength)
            return $"StudentNumber no puede superar {MaxStudentNumberLength} caracteres.";

        if (!seenEmails.Add(row.Email))
            return "El email esta duplicado en el archivo.";

        return null;
    }

    private void ImportRow(StudentCsvRow row, int centerId, Course course)
    {
        var user = User.Create(row.Email!, row.FirstName!, row.LastName!);
        user.AssignRole(centerId, UserRole.Student);
        var student = Student.Create(user, centerId, row.StudentNumber);
        course.EnrollStudent(student, null);

        userRepository.Add(user);
        studentRepository.Add(student);
    }

    private static Result<List<StudentCsvRow>> ParseCsv(string? content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Error.Validation("csv.empty", "El archivo CSV esta vacio.");

        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
        };

        var rows = new List<StudentCsvRow>();
        var isFirstRecord = true;
        var dataRowNumber = 0;
        var emailIndex = -1;
        var studentNumberIndex = -1;
        var firstNameIndex = -1;
        var lastNameIndex = -1;

        try
        {
            using var reader = new StringReader(content);
            using var csv = new CsvReader(reader, configuration);

            while (csv.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var record = csv.Parser.Record ?? [];

                if (isFirstRecord)
                {
                    emailIndex = FindColumn(record, "Email");
                    studentNumberIndex = FindColumn(record, "StudentNumber");
                    firstNameIndex = FindColumn(record, "FirstName");
                    lastNameIndex = FindColumn(record, "LastName");

                    if (emailIndex < 0 || firstNameIndex < 0 || lastNameIndex < 0)
                        return Error.Validation("csv.invalid_header", "La cabecera debe incluir las columnas Email, FirstName y LastName (StudentNumber es opcional).");

                    isFirstRecord = false;
                    continue;
                }

                dataRowNumber++;
                if (dataRowNumber > MaxImportRows)
                    return Error.Validation("csv.too_many_rows", $"El archivo supera el maximo de {MaxImportRows} filas por importacion.");

                rows.Add(new StudentCsvRow(dataRowNumber + 1,
                                           ReadField(record, emailIndex),
                                           ReadField(record, studentNumberIndex),
                                           ReadField(record, firstNameIndex),
                                           ReadField(record, lastNameIndex)));
            }
        }
        catch (CsvHelperException exception)
        {
            return Error.Validation("csv.malformed", $"El archivo CSV tiene un formato invalido: {exception.Message}");
        }

        if (rows.Count == 0)
            return Error.Validation("csv.empty", "El archivo CSV no contiene filas de datos.");

        return rows;
    }

    private static int FindColumn(string?[] record, string name) =>
        Array.FindIndex(record, column => string.Equals(column, name, StringComparison.OrdinalIgnoreCase));

    private static string? ReadField(string?[] record, int index) =>
        index >= 0 && index < record.Length && !string.IsNullOrWhiteSpace(record[index]) ? record[index] : null;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex StudentEmailRegex();
}

public sealed class ImportStudentsCsvCommandValidator : AbstractValidator<ImportStudentsCsvCommand>
{
    public ImportStudentsCsvCommandValidator()
    {
        RuleFor(x => x.CenterId).GreaterThan(0);
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.CsvContent).NotEmpty().WithMessage("Se debe adjuntar un archivo CSV.");
    }
}
