using System.ComponentModel.DataAnnotations;
using melody.Models.Domain;
using melody.Models.ViewModels;

namespace melody.Tests;

/// <summary>
/// Unit tests for <see cref="StudentFormViewModel"/>'s <c>[Range(1, 8)]</c> on <see cref="StudentFormViewModel.Class"/>
/// (FR-009's class input), validated at the cheapest layer (DataAnnotations directly, no HTTP
/// round-trip through AdminStudentsController). Pure unit tests: no DB/host required.
/// </summary>
public class StudentFormValidationTests
{
    private static StudentFormViewModel CreateValidModel(int studentClass) => new()
    {
        FullName = "Test Student",
        Class = studentClass,
        Instrument = Instrument.Piano,
        TeacherId = 1,
    };

    private static bool TryValidate(StudentFormViewModel model, out List<ValidationResult> results)
    {
        results = [];
        return Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
    }

    [Fact]
    public void Validate_ClassZero_FailsWithClassValidationResult()
    {
        var model = CreateValidModel(0);

        var isValid = TryValidate(model, out var results);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(StudentFormViewModel.Class)));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(-1)]
    public void Validate_ClassOutOfRange_FailsWithClassValidationResult(int studentClass)
    {
        var model = CreateValidModel(studentClass);

        var isValid = TryValidate(model, out var results);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(StudentFormViewModel.Class)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Validate_ClassWithinValidRange_Succeeds(int studentClass)
    {
        var model = CreateValidModel(studentClass);

        var isValid = TryValidate(model, out var results);

        Assert.True(isValid);
    }
}
