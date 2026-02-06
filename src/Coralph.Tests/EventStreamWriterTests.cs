using System.Text.Json;
using Coralph;

namespace Coralph.Tests;

public class EventStreamWriterTests
{
    [Fact]
    public void WriteSessionHeader_EmitsSessionEnvelope()
    {
        var sw = new StringWriter();
        var writer = new EventStreamWriter(sw, "session-123");

        writer.WriteSessionHeader("/tmp/workspace");

        var line = sw.ToString().Trim();
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        Assert.Equal("session", root.GetProperty("type").GetString());
        Assert.Equal("session-123", root.GetProperty("id").GetString());
        Assert.Equal("/tmp/workspace", root.GetProperty("cwd").GetString());
        Assert.True(root.TryGetProperty("timestamp", out _));
    }

    [Fact]
    public void Emit_AddsSessionIdAndType()
    {
        var sw = new StringWriter();
        var writer = new EventStreamWriter(sw, "session-456");

        writer.Emit("message_update", turn: 2, messageId: "msg-1", fields: new Dictionary<string, object?>
        {
            ["delta"] = "hello"
        });

        var line = sw.ToString().Trim();
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        Assert.Equal("message_update", root.GetProperty("type").GetString());
        Assert.Equal("session-456", root.GetProperty("sessionId").GetString());
        Assert.Equal(2, root.GetProperty("turn").GetInt32());
        Assert.Equal("msg-1", root.GetProperty("messageId").GetString());
        Assert.Equal("hello", root.GetProperty("delta").GetString());
    }

    [Fact]
    public void Emit_DoesNotFlushOnEveryEventByDefault()
    {
        var trackingWriter = new TrackingWriter();
        var writer = new EventStreamWriter(trackingWriter, "session-789");

        writer.Emit("message_update", turn: 1, fields: new Dictionary<string, object?>
        {
            ["delta"] = "chunk"
        });

        Assert.Equal(0, trackingWriter.FlushCount);
    }

    [Fact]
    public void Emit_FlushesOnTurnEndBoundary()
    {
        var trackingWriter = new TrackingWriter();
        var writer = new EventStreamWriter(trackingWriter, "session-101");

        writer.Emit("message_update", turn: 1, fields: new Dictionary<string, object?>
        {
            ["delta"] = "chunk"
        });
        writer.Emit("turn_end", turn: 1, fields: new Dictionary<string, object?>
        {
            ["success"] = true
        });

        Assert.Equal(1, trackingWriter.FlushCount);
    }

    private sealed class TrackingWriter : StringWriter
    {
        internal int FlushCount { get; private set; }

        public override void Flush()
        {
            FlushCount++;
            base.Flush();
        }
    }
}
