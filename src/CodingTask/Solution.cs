namespace CodingTask;

public static class Solution
{
    public static int Solve(int[] input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var sum = 0;
        foreach (var x in input)
            sum += x;
        return sum;
    }
}
