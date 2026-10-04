namespace CodingTask;

public sealed record PageResult(
    IReadOnlyList<Uri> Links,
    Exception? Error);
