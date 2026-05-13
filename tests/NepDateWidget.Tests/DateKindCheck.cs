using NepDate;
using System;
using Xunit;

namespace NepDateWidget.Tests;

public class DateKindCheck
{
    [Fact]
    public void NepDate_EnglishDate_Kind()
    {
        var n = new NepaliDate(2082, 12, 20);
        // This assertion tells us what Kind the NepDate uses
        Assert.Equal(DateTimeKind.Unspecified, n.EnglishDate.Kind);
    }
}
