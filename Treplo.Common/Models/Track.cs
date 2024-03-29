﻿namespace Treplo.Common.Models;

[GenerateSerializer]
public readonly record struct Track(
    string Title,
    Thumbnail Thumbnail,
    StreamInfo Source,
    string Author,
    TimeSpan Duration
)
{
    public override string ToString() => $"Track ({Title} - {Author})";
}