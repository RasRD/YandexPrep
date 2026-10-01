namespace CodingTask.Tests;

public class SolutionTests
{
    [Fact]
    public void Solve_EmptyArray_ReturnsZero()
    {
        Assert.Equal(0, Solution.Solve([]));
    }

    [Fact]
    public void Solve_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Solution.Solve(null!));
    }

    [Theory]
    [InlineData(new[] { 1 }, 1)]
    [InlineData(new[] { 1, 2, 3 }, 6)]
    [InlineData(new[] { -5, 5 }, 0)]
    public void Solve_ReturnsExpected(int[] input, int expected)
    {
        Assert.Equal(expected, Solution.Solve(input));
    }
}
