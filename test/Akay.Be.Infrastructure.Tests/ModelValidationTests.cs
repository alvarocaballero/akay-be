using System.Linq;
using Akay.Be.Domain.Aggregates.Academic;
using Akay.Be.Domain.Aggregates.Identity;
using Akay.Be.Domain.Aggregates.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Akay.Be.Infrastructure.Tests;

public class ModelValidationTests
{
    [Fact]
    public void ModelBuildsWithoutErrors()
    {
        using var context = TestDbContextFactory.CreateContext();

        var model = context.Model;

        Assert.NotNull(model);
    }

    [Fact]
    public void OrganizationIsMappedToOrganizationSchema()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Domain.Aggregates.Organization.Organization))!;

        Assert.Equal("Organization", entityType.GetTableName());
        Assert.Equal("organization", entityType.GetSchema());
    }

    [Fact]
    public void UserIsMappedToIdentitySchema()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(User))!;

        Assert.Equal("User", entityType.GetTableName());
        Assert.Equal("identity", entityType.GetSchema());
    }

    [Fact]
    public void AcademicPeriodIsMappedToAcademicSchema()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(AcademicPeriod))!;

        Assert.Equal("AcademicPeriod", entityType.GetTableName());
        Assert.Equal("academic", entityType.GetSchema());
    }

    [Fact]
    public void SubjectHasOrganizationIdForeignKey()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Subject))!;
        var fk = entityType.GetForeignKeys()
            .Single(fk => fk.GetConstraintName() == "FK_Subject_Organization_OrganizationId");

        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    [Fact]
    public void AcademicPeriodHasCenterIdForeignKey()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(AcademicPeriod))!;
        var fk = entityType.GetForeignKeys()
            .Single(fk => fk.GetConstraintName() == "FK_AcademicPeriod_Organization_CenterId");

        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    [Fact]
    public void AdminCourseSubjectIsMappedToAdminCourseSubjectTable()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(AdminCourseSubject))!;

        Assert.Equal("AdminCourseSubject", entityType.GetTableName());
        Assert.Equal("academic", entityType.GetSchema());
    }

    [Fact]
    public void AdminCourseSubjectHasUserIdForeignKey()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(AdminCourseSubject))!;
        var fk = entityType.GetForeignKeys()
            .Single(fk => fk.GetConstraintName() == "FK_AdminCourseSubject_User_UserId");

        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    [Fact]
    public void AdminCourseSubjectHasUniqueIndexOnCourseSubjectAndUser()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(AdminCourseSubject))!;
        var index = entityType.GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_AdminCourseSubject_CourseSubjectId_UserId");

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void TeacherEntityIsNotInModel()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType("Teacher");
        Assert.Null(entityType);
    }

    [Fact]
    public void UserRoleAssignmentHasScopedFilteredUniqueIndex()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(UserRoleAssignment))!;
        var index = entityType.GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_UserRoleAssignment_Scoped");

        Assert.True(index.IsUnique);
        Assert.Equal("[OrganizationId] IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public void UserRoleAssignmentHasGlobalFilteredUniqueIndex()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(UserRoleAssignment))!;
        var index = entityType.GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_UserRoleAssignment_Global");

        Assert.True(index.IsUnique);
        Assert.Equal("[OrganizationId] IS NULL", index.GetFilter());
    }

    [Fact]
    public void CourseIsMappedToAcademicSchema()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Course))!;

        Assert.Equal("Course", entityType.GetTableName());
        Assert.Equal("academic", entityType.GetSchema());
        Assert.Contains("FK_Course_Organization_CenterId",
            entityType.GetForeignKeys().Select(fk => fk.GetConstraintName()));

        var uniqueIndex = entityType.GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_Course_CenterId_AcademicPeriodId_Name");
        Assert.True(uniqueIndex.IsUnique);
    }

    [Fact]
    public void StudentIsMappedToAcademicSchema()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Student))!;

        Assert.Equal("Student", entityType.GetTableName());
        Assert.Equal("academic", entityType.GetSchema());

        var centerFk = entityType.GetForeignKeys()
            .Single(fk => fk.GetConstraintName() == "FK_Student_Organization_CenterId");
        Assert.Equal(DeleteBehavior.Restrict, centerFk.DeleteBehavior);

        Assert.Contains(entityType.GetIndexes(),
            i => i.GetDatabaseName() == "UX_Student_UserId" && i.IsUnique);
    }

    [Fact]
    public void StudentCourseIsMappedToAcademicSchema()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(StudentCourse))!;

        Assert.Equal("StudentCourse", entityType.GetTableName());
        Assert.Equal("academic", entityType.GetSchema());

        Assert.True(entityType.GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_StudentCourse_StudentId_CourseId").IsUnique);
    }

    [Fact]
    public void StudentCourseSubjectIsMappedToAcademicSchema()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(StudentCourseSubject))!;

        Assert.Equal("StudentCourseSubject", entityType.GetTableName());
        Assert.Equal("academic", entityType.GetSchema());

        Assert.True(entityType.GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_StudentCourseSubject_StudentCourseId_CourseSubjectId").IsUnique);
    }

    [Fact]
    public void CourseSubjectHasRequiredUniqueIndex()
    {
        using var context = TestDbContextFactory.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(CourseSubject))!;

        var uniqueIndex = entityType.GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_CourseSubject_CourseId_SubjectId");
        Assert.True(uniqueIndex.IsUnique);
    }

    [Fact]
    public void AllBusinessTablesHaveCorrectDeleteBehaviors()
    {
        using var context = TestDbContextFactory.CreateContext();

        // FK from Student.CenterId should be Restrict (user cant be deleted if students reference them)
        var studentFk = context.Model.FindEntityType(typeof(Student))!
            .GetForeignKeys().Single(fk => fk.GetConstraintName() == "FK_Student_User_UserId");
        Assert.Equal(DeleteBehavior.Restrict, studentFk.DeleteBehavior);

        // FK from AdminCourseSubject.UserId should be Restrict
        var adminFk = context.Model.FindEntityType(typeof(AdminCourseSubject))!
            .GetForeignKeys().Single(fk => fk.GetConstraintName() == "FK_AdminCourseSubject_User_UserId");
        Assert.Equal(DeleteBehavior.Restrict, adminFk.DeleteBehavior);
    }
}
