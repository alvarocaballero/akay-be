using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Entities.Organization;
using Akay.Be.Domain.Enums;
using Akay.Be.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Seed;

public static class DevelopmentSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Set<Center>().AnyAsync(cancellationToken))
            return;

        // ── Centers ──────────────────────────────────────────────────────────
        var centroNorte = Center.Create("Centro Norte", "NORTH");
        var centroSur = Center.Create("Centro Sur", "SOUTH");
        var centroInternacional = Center.Create("Centro Internacional", "INTL");

        context.Set<Center>().AddRange(centroNorte, centroSur, centroInternacional);
        await context.SaveChangesAsync(cancellationToken);

        // ── SuperAdmin ───────────────────────────────────────────────────────
        var superAdmin = User.Create("superadmin@example.com", "Super", "Admin");
        superAdmin.AssignGlobalRole(UserRole.SuperAdmin);
        context.Set<User>().Add(superAdmin);
        await context.SaveChangesAsync(cancellationToken);

        // ── Admin users ──────────────────────────────────────────────────────
        var adminGeneral = User.Create("admin.general@example.com", "Admin", "General");
        adminGeneral.AssignRole(centroNorte.Id, UserRole.Admin);
        adminGeneral.AssignRole(centroSur.Id, UserRole.Admin);

        var adminSur = User.Create("admin.sur@example.com", "Admin", "Sur");
        adminSur.AssignRole(centroSur.Id, UserRole.Admin);

        var adminInternacional = User.Create("admin.internacional@example.com", "Admin", "Internacional");
        adminInternacional.AssignRole(centroInternacional.Id, UserRole.Admin);

        var adminTeacher = User.Create("admin.teacher@example.com", "Admin", "Teacher");
        adminTeacher.AssignRole(centroNorte.Id, UserRole.Admin);
        adminTeacher.AssignRole(centroNorte.Id, UserRole.Teacher);
        adminTeacher.AssignRole(centroSur.Id, UserRole.Teacher);

        context.Set<User>().AddRange(adminGeneral, adminSur, adminInternacional, adminTeacher);
        await context.SaveChangesAsync(cancellationToken);

        // ── Teacher users ────────────────────────────────────────────────────
        var teacherMath = User.Create("teacher.math@example.com", "Teacher", "Math");
        teacherMath.AssignRole(centroNorte.Id, UserRole.Teacher);
        teacherMath.AssignRole(centroSur.Id, UserRole.Teacher);

        var teacherScience = User.Create("teacher.science@example.com", "Teacher", "Science");
        teacherScience.AssignRole(centroNorte.Id, UserRole.Teacher);
        teacherScience.AssignRole(centroSur.Id, UserRole.Teacher);
        teacherScience.AssignRole(centroInternacional.Id, UserRole.Teacher);

        var teacherEnglish = User.Create("teacher.english@example.com", "Teacher", "English");
        teacherEnglish.AssignRole(centroInternacional.Id, UserRole.Teacher);

        var teacherHistory = User.Create("teacher.history@example.com", "Teacher", "History");
        teacherHistory.AssignRole(centroNorte.Id, UserRole.Teacher);

        var teacherSouth = User.Create("teacher.south@example.com", "Teacher", "South");
        teacherSouth.AssignRole(centroSur.Id, UserRole.Teacher);

        context.Set<User>().AddRange(teacherMath, teacherScience, teacherEnglish, teacherHistory, teacherSouth);
        await context.SaveChangesAsync(cancellationToken);

        // ── Student users ────────────────────────────────────────────────────
        var s01 = User.Create("student01@example.com", "Student", "One");
        var s02 = User.Create("student02@example.com", "Student", "Two");
        var s03 = User.Create("student03@example.com", "Student", "Three");
        var s04 = User.Create("student04@example.com", "Student", "Four");
        var s05 = User.Create("student05@example.com", "Student", "Five");
        var s06 = User.Create("student06@example.com", "Student", "Six");
        var s07 = User.Create("student07@example.com", "Student", "Seven");
        var s08 = User.Create("student08@example.com", "Student", "Eight");
        var s09 = User.Create("student09@example.com", "Student", "Nine");
        var s10 = User.Create("student10@example.com", "Student", "Ten");

        context.Set<User>().AddRange(s01, s02, s03, s04, s05, s06, s07, s08, s09, s10);
        await context.SaveChangesAsync(cancellationToken);

        // ── Student role assignments and Student profiles ────────────────────
        var studentData = new[]
        {
            (User: s01, Center: centroNorte),
            (User: s02, Center: centroNorte),
            (User: s03, Center: centroNorte),
            (User: s04, Center: centroNorte),
            (User: s03, Center: centroSur),
            (User: s05, Center: centroSur),
            (User: s06, Center: centroSur),
            (User: s07, Center: centroSur),
            (User: s07, Center: centroInternacional),
            (User: s08, Center: centroInternacional),
            (User: s09, Center: centroInternacional),
            (User: s10, Center: centroInternacional),
        };

        var studentRecords = new List<Student>();
        foreach (var (user, center) in studentData)
        {
            user.AssignRole(center.Id, UserRole.Student);
            studentRecords.Add(Student.Create(user.Id, center.Id));
        }
        context.Set<Student>().AddRange(studentRecords);
        await context.SaveChangesAsync(cancellationToken);

        var studentByUserCenter = studentRecords.ToDictionary(s => (s.UserId, s.CenterId));

        // ── Academic periods ─────────────────────────────────────────────────
        var periodNorte = AcademicPeriod.Create(centroNorte.Id, "2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        var periodSur = AcademicPeriod.Create(centroSur.Id, "2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        var periodIntl = AcademicPeriod.Create(centroInternacional.Id, "2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));

        var oldPeriodNorte = AcademicPeriod.Create(centroNorte.Id, "2025-2026", new DateOnly(2025, 9, 1), new DateOnly(2026, 6, 30));
        oldPeriodNorte.Deactivate();
        var oldPeriodSur = AcademicPeriod.Create(centroSur.Id, "2025-2026", new DateOnly(2025, 9, 1), new DateOnly(2026, 6, 30));
        oldPeriodSur.Deactivate();
        var oldPeriodIntl = AcademicPeriod.Create(centroInternacional.Id, "2025-2026", new DateOnly(2025, 9, 1), new DateOnly(2026, 6, 30));
        oldPeriodIntl.Deactivate();

        context.Set<AcademicPeriod>().AddRange(periodNorte, periodSur, periodIntl, oldPeriodNorte, oldPeriodSur, oldPeriodIntl);
        await context.SaveChangesAsync(cancellationToken);

        // ── Courses ──────────────────────────────────────────────────────────
        var curso1Norte = Course.Create(periodNorte.Id, "1º ESO", "ESO1");
        var curso2Norte = Course.Create(periodNorte.Id, "2º ESO", "ESO2");
        var curso1Sur = Course.Create(periodSur.Id, "1º ESO", "ESO1");
        var curso2Sur = Course.Create(periodSur.Id, "2º ESO", "ESO2");
        var curso1Intl = Course.Create(periodIntl.Id, "1º ESO", "ESO1");
        var curso2Intl = Course.Create(periodIntl.Id, "2º ESO", "ESO2");

        context.Set<Course>().AddRange(curso1Norte, curso2Norte, curso1Sur, curso2Sur, curso1Intl, curso2Intl);
        await context.SaveChangesAsync(cancellationToken);

        // ── Subjects ─────────────────────────────────────────────────────────
        var matematicasCompartidas = Subject.Create("Matemáticas compartidas", null, [centroNorte.Id, centroSur.Id]);
        var cienciasComunes = Subject.Create("Ciencias comunes", null, [centroNorte.Id, centroSur.Id, centroInternacional.Id]);
        var historiaNorte = Subject.Create("Historia Norte", null, [centroNorte.Id]);
        var inglesInternacional = Subject.Create("Inglés Internacional", null, [centroInternacional.Id]);
        var matematicasInternacional = Subject.Create("Matemáticas Internacional", null, [centroInternacional.Id]);

        context.Set<Subject>().AddRange(matematicasCompartidas, cienciasComunes, historiaNorte, inglesInternacional, matematicasInternacional);
        await context.SaveChangesAsync(cancellationToken);

        // ── Subject admins ───────────────────────────────────────────────────
        matematicasCompartidas.AddAdmin(adminGeneral.Id);
        matematicasCompartidas.AddAdmin(teacherMath.Id);

        cienciasComunes.AddAdmin(teacherScience.Id);
        cienciasComunes.AddAdmin(adminInternacional.Id);

        historiaNorte.AddAdmin(teacherHistory.Id);

        inglesInternacional.AddAdmin(teacherEnglish.Id);

        matematicasInternacional.AddAdmin(adminInternacional.Id);

        await context.SaveChangesAsync(cancellationToken);

        // ── Course subjects ──────────────────────────────────────────────────
        void AddToCourse(Course course, Subject subject) => course.AddSubject(subject.Id);

        AddToCourse(curso1Norte, matematicasCompartidas);
        AddToCourse(curso1Norte, cienciasComunes);
        AddToCourse(curso1Norte, historiaNorte);

        AddToCourse(curso1Sur, matematicasCompartidas);
        AddToCourse(curso1Sur, cienciasComunes);

        AddToCourse(curso1Intl, cienciasComunes);
        AddToCourse(curso1Intl, inglesInternacional);
        AddToCourse(curso1Intl, matematicasInternacional);

        AddToCourse(curso2Norte, matematicasCompartidas);
        AddToCourse(curso2Norte, cienciasComunes);

        AddToCourse(curso2Sur, matematicasCompartidas);

        AddToCourse(curso2Intl, cienciasComunes);
        AddToCourse(curso2Intl, inglesInternacional);

        await context.SaveChangesAsync(cancellationToken);

        // ── Teachers per course-subject ──────────────────────────────────────
        void AssignTeacherToCs(Course course, int subjectId, int userId)
        {
            var cs = course.Subjects.First(s => s.SubjectId == subjectId);
            cs.AssignTeacher(userId);
        }

        AssignTeacherToCs(curso1Norte, matematicasCompartidas.Id, teacherMath.Id);
        AssignTeacherToCs(curso1Norte, matematicasCompartidas.Id, adminTeacher.Id);
        AssignTeacherToCs(curso1Norte, cienciasComunes.Id, teacherScience.Id);
        AssignTeacherToCs(curso1Norte, historiaNorte.Id, teacherHistory.Id);

        AssignTeacherToCs(curso1Sur, matematicasCompartidas.Id, teacherMath.Id);
        AssignTeacherToCs(curso1Sur, cienciasComunes.Id, teacherScience.Id);
        AssignTeacherToCs(curso1Sur, cienciasComunes.Id, teacherSouth.Id);

        AssignTeacherToCs(curso1Intl, cienciasComunes.Id, teacherScience.Id);
        AssignTeacherToCs(curso1Intl, inglesInternacional.Id, teacherEnglish.Id);
        AssignTeacherToCs(curso1Intl, matematicasInternacional.Id, adminInternacional.Id);

        AssignTeacherToCs(curso2Norte, matematicasCompartidas.Id, teacherMath.Id);
        AssignTeacherToCs(curso2Norte, cienciasComunes.Id, teacherScience.Id);

        AssignTeacherToCs(curso2Sur, matematicasCompartidas.Id, teacherMath.Id);

        AssignTeacherToCs(curso2Intl, cienciasComunes.Id, teacherScience.Id);
        AssignTeacherToCs(curso2Intl, inglesInternacional.Id, adminInternacional.Id);

        await context.SaveChangesAsync(cancellationToken);

        // ── Student enrollments in courses ───────────────────────────────────
        void EnrollStudentInCourse(Course course, int userId, int centerId)
        {
            if (studentByUserCenter.ContainsKey((userId, centerId)))
                course.EnrollStudent(userId);
        }

        EnrollStudentInCourse(curso1Norte, s01.Id, centroNorte.Id);
        EnrollStudentInCourse(curso1Norte, s02.Id, centroNorte.Id);
        EnrollStudentInCourse(curso1Norte, s03.Id, centroNorte.Id);

        EnrollStudentInCourse(curso2Norte, s04.Id, centroNorte.Id);

        EnrollStudentInCourse(curso1Sur, s03.Id, centroSur.Id);
        EnrollStudentInCourse(curso1Sur, s05.Id, centroSur.Id);
        EnrollStudentInCourse(curso1Sur, s06.Id, centroSur.Id);
        EnrollStudentInCourse(curso1Sur, s07.Id, centroSur.Id);

        EnrollStudentInCourse(curso1Intl, s07.Id, centroInternacional.Id);
        EnrollStudentInCourse(curso1Intl, s08.Id, centroInternacional.Id);

        EnrollStudentInCourse(curso2Intl, s09.Id, centroInternacional.Id);
        EnrollStudentInCourse(curso2Intl, s10.Id, centroInternacional.Id);

        await context.SaveChangesAsync(cancellationToken);

        // ── Course subject student enrollments ───────────────────────────────
        void EnrollInAllSubjects(Course course, int userId, int centerId)
        {
            if (!studentByUserCenter.ContainsKey((userId, centerId)))
                return;

            var sc = course.Students.First(s => s.UserId == userId);
            foreach (var cs in course.Subjects)
                cs.EnrollStudent(sc.Id);
        }

        void EnrollInSubjects(Course course, int userId, int centerId, params Subject[] subjects)
        {
            if (!studentByUserCenter.ContainsKey((userId, centerId)))
                return;

            var subjectIds = subjects.Select(s => s.Id).ToHashSet();
            var sc = course.Students.First(s => s.UserId == userId);
            foreach (var cs in course.Subjects.Where(s => subjectIds.Contains(s.SubjectId)))
                cs.EnrollStudent(sc.Id);
        }

        EnrollInAllSubjects(curso1Norte, s01.Id, centroNorte.Id);
        EnrollInAllSubjects(curso1Norte, s02.Id, centroNorte.Id);
        EnrollInSubjects(curso1Norte, s03.Id, centroNorte.Id, matematicasCompartidas, cienciasComunes);

        EnrollInAllSubjects(curso1Sur, s05.Id, centroSur.Id);
        EnrollInAllSubjects(curso1Sur, s06.Id, centroSur.Id);
        EnrollInSubjects(curso1Sur, s03.Id, centroSur.Id, matematicasCompartidas);

        EnrollInAllSubjects(curso1Intl, s08.Id, centroInternacional.Id);
        EnrollInSubjects(curso1Intl, s07.Id, centroInternacional.Id, cienciasComunes, inglesInternacional);

        await context.SaveChangesAsync(cancellationToken);
    }
}
