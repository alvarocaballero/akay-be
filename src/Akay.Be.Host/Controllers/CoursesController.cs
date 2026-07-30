using Akay.Be.Application.Features.Courses;
using Akay.Be.Application.Features.CourseStudents;
using Akay.Be.Application.Features.CourseSubjectStudents;
using Akay.Be.Application.Features.CourseSubjectTeachers;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable CS1591

namespace Akay.Be.Host.Controllers;

/// <summary>
/// Gestión de cursos, asignaturas, matrículas y profesores.
/// </summary>
[ApiController]
[Route("api/courses")]
[Tags("Courses")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class CoursesController(IDispatcher dispatcher) : ControllerBase
{
    // ─── Courses ───────────────────────────────────────────────────────────

    [HttpGet]
    [EndpointSummary("Lista los cursos del centro indicado en el header X-Center-Id, con filtro opcional por periodo académico.")]
    [ProducesResponseType<IReadOnlyList<CourseListResponse>>(StatusCodes.Status200OK)]
    public async Task<IResult> GetAll([FromHeader(Name = "X-Center-Id")] int centerId,
                                      [FromQuery] int? academicPeriodId,
                                      CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetCoursesQuery(centerId, academicPeriodId), cancellationToken)).ToOk();

    [HttpGet("{id:int}")]
    [EndpointSummary("Obtiene un curso por su ID.")]
    [ProducesResponseType<CourseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetById(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetCourseByIdQuery(id), cancellationToken)).ToOk();

    [HttpPost]
    [EndpointSummary("Crea un curso en un periodo académico administrado.")]
    [ProducesResponseType<CreatedResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create([FromBody] CreateCourseCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command, cancellationToken)).ToCreated(value => $"api/courses/{value.Id}");

    [HttpPut("{id:int}")]
    [EndpointSummary("Actualiza un curso visible.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(int id, [FromBody] UpdateCourseCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { Id = id }, cancellationToken)).ToNoContent();

    [HttpDelete("{id:int}")]
    [EndpointSummary("Elimina un curso visible (soft-delete).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteCourseCommand(id), cancellationToken)).ToNoContent();

    // ─── Course Subjects ───────────────────────────────────────────────────

    [HttpPost("{courseId:int}/subjects")]
    [EndpointSummary("Asigna una asignatura disponible a un curso visible.")]
    [ProducesResponseType<CreatedResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> AddSubject(int courseId, [FromBody] AddCourseSubjectCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { CourseId = courseId }, cancellationToken)).ToCreated($"api/courses/{courseId}/subjects");

    [HttpDelete("{courseId:int}/subjects/{subjectId:int}")]
    [EndpointSummary("Elimina una asignatura asignada de un curso visible.")]
    [ProducesResponseType<CourseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> RemoveSubject(int courseId, int subjectId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new RemoveCourseSubjectCommand(courseId, subjectId), cancellationToken)).ToOk();

    // ─── Course Students ───────────────────────────────────────────────────

    [HttpGet("{courseId:int}/students")]
    [EndpointSummary("Lista los estudiantes matriculados en un curso visible.")]
    [ProducesResponseType<IReadOnlyList<CourseStudentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetStudents(int courseId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetCourseStudentsQuery(courseId), cancellationToken)).ToOk();

    [HttpPost("{courseId:int}/students")]
    [EndpointSummary("Matricula un estudiante del mismo centro en un curso visible.")]
    [ProducesResponseType<CreatedResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> EnrollStudent(int courseId, [FromBody] EnrollCourseStudentCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { CourseId = courseId }, cancellationToken)).ToCreated($"api/courses/{courseId}/students");

    [HttpDelete("{courseId:int}/students/{studentId:int}")]
    [EndpointSummary("Desmatricula un estudiante de un curso visible.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> UnenrollStudent(int courseId, int studentId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new UnenrollCourseStudentCommand(courseId, studentId), cancellationToken)).ToNoContent();

    // ─── Course Subject Teachers ───────────────────────────────────────────

    [HttpGet("{courseId:int}/subjects/{subjectId:int}/teachers")]
    [EndpointSummary("Lista los profesores asignados a una asignatura de un curso visible.")]
    [ProducesResponseType<IReadOnlyList<CourseSubjectTeacherResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetSubjectTeachers(int courseId, int subjectId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetCourseSubjectTeachersQuery(courseId, subjectId), cancellationToken)).ToOk();

    [HttpPost("{courseId:int}/subjects/{subjectId:int}/teachers")]
    [EndpointSummary("Asigna un profesor con rol Teacher en el centro a una asignatura de un curso visible.")]
    [ProducesResponseType<CreatedResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> AssignSubjectTeacher(int courseId, int subjectId, [FromBody] AssignCourseSubjectTeacherCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { CourseId = courseId, SubjectId = subjectId }, cancellationToken)).ToCreated($"api/courses/{courseId}/subjects/{subjectId}/teachers");

    [HttpDelete("{courseId:int}/subjects/{subjectId:int}/teachers/{userId:int}")]
    [EndpointSummary("Desasigna un profesor de una asignatura de un curso visible.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> RemoveSubjectTeacher(int courseId, int subjectId, int userId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new RemoveCourseSubjectTeacherCommand(courseId, subjectId, userId), cancellationToken)).ToNoContent();

    // ─── Course Subject Students ───────────────────────────────────────────

    [HttpGet("{courseId:int}/subjects/{subjectId:int}/students")]
    [EndpointSummary("Lista los estudiantes inscritos en una asignatura de un curso visible.")]
    [ProducesResponseType<IReadOnlyList<CourseSubjectStudentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetSubjectStudents(int courseId, int subjectId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetCourseSubjectStudentsQuery(courseId, subjectId), cancellationToken)).ToOk();

    [HttpPost("{courseId:int}/subjects/{subjectId:int}/students")]
    [EndpointSummary("Inscribe un estudiante matriculado en el curso a una asignatura del curso visible.")]
    [ProducesResponseType<CreatedResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> EnrollSubjectStudent(int courseId, int subjectId, [FromBody] EnrollCourseSubjectStudentCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { CourseId = courseId, SubjectId = subjectId }, cancellationToken)).ToCreated($"api/courses/{courseId}/subjects/{subjectId}/students");

    [HttpDelete("{courseId:int}/subjects/{subjectId:int}/students/{studentId:int}")]
    [EndpointSummary("Desinscribe un estudiante de una asignatura de un curso visible.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> UnenrollSubjectStudent(int courseId, int subjectId, int studentId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new UnenrollCourseSubjectStudentCommand(courseId, subjectId, studentId), cancellationToken)).ToNoContent();
}

#pragma warning restore CS1591
