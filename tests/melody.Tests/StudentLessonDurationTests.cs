using melody.Models.Domain;

namespace melody.Tests;

/// <summary>
/// Unit tests for <see cref="Student.LessonDuration"/>, the FR-009 rule deriving lesson duration
/// from class (never user input): 20 minutes for classes 1-4, 40 minutes for classes 5-8. Pure
/// unit tests: no DB/host required.
/// </summary>
public class StudentLessonDurationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void LessonDuration_Classes1To4_Returns20Minutes(int studentClass)
    {
        var student = new Student { Class = studentClass };

        Assert.Equal(TimeSpan.FromMinutes(20), student.LessonDuration);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void LessonDuration_Classes5To8_Returns40Minutes(int studentClass)
    {
        var student = new Student { Class = studentClass };

        Assert.Equal(TimeSpan.FromMinutes(40), student.LessonDuration);
    }

    [Fact]
    public void LessonDuration_AtClass4To5Boundary_SwitchesFrom20To40Minutes()
    {
        var classFour = new Student { Class = 4 };
        var classFive = new Student { Class = 5 };

        Assert.Equal(TimeSpan.FromMinutes(20), classFour.LessonDuration);
        Assert.Equal(TimeSpan.FromMinutes(40), classFive.LessonDuration);
    }
}
