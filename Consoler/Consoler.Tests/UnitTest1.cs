using Consoler;
namespace Consoler.Tests;

public class UnitTest1
{
    [Fact]
    public void TestHeaderProcessing()
    {
        string header = "Nome                          ID                              Versão              Disponível              Origem";
        string info = "Spotify                       Spotify.Spotify                 1.3.3.264.gdaf3b824 1.3.4.258.gc60b10b0946a winget";

        var table = new TextTable(header.AsSpan());

        Assert.Equal(5, table.Columns.Count);

        var values = table.ReadRow(info.AsSpan());
        Assert.Equal(5, values.Count);

        Assert.Equal("Spotify", values[0]);
        Assert.Equal("Spotify.Spotify", values[1]);
        Assert.Equal("1.3.3.264.gdaf3b824", values[2]);
        Assert.Equal("1.3.4.258.gc60b10b0946a", values[3]);
        Assert.Equal("winget", values[4]);

    }

    [Fact]
    public void TestHeaderProcessing_TrickEnding()
    {
        string header = "Nome                          ID                              Versão              Disponível              Origem ";
        string info = "Spotify                       Spotify.Spotify                 1.3.3.264.gdaf3b824 1.3.4.258.gc60b10b0946a winget";

        var table = new TextTable(header.AsSpan());

        Assert.Equal(5, table.Columns.Count);

        var values = table.ReadRow(info.AsSpan());
        Assert.Equal(5, values.Count);

        Assert.Equal("Spotify", values[0]);
        Assert.Equal("Spotify.Spotify", values[1]);
        Assert.Equal("1.3.3.264.gdaf3b824", values[2]);
        Assert.Equal("1.3.4.258.gc60b10b0946a", values[3]);
        Assert.Equal("winget", values[4]);

    }


    [Fact]
    public void TestHeaderProcessing_TrickEnding1()
    {
        string header = "Nome                          ID                              Versão              Disponível              Origem    ";
        string info = "Spotify                       Spotify.Spotify                 1.3.3.264.gdaf3b824 1.3.4.258.gc60b10b0946a winget123";

        var table = new TextTable(header.AsSpan());

        Assert.Equal(5, table.Columns.Count);

        var values = table.ReadRow(info.AsSpan());
        Assert.Equal(5, values.Count);

        Assert.Equal("Spotify", values[0]);
        Assert.Equal("Spotify.Spotify", values[1]);
        Assert.Equal("1.3.3.264.gdaf3b824", values[2]);
        Assert.Equal("1.3.4.258.gc60b10b0946a", values[3]);
        Assert.Equal("winget123", values[4]);

    }
}
