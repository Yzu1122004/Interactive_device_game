using System;
using System.Text;

public enum PrototypeVoiceCommand
{
    None, Forward, Backward, Left, Right, Stop, UTurn,
    NudgeForward, NudgeBackward, NudgeLeft, NudgeRight
}

// Match complete commands. Negations and multiple commands are not substring matches.
public static class PrototypeVoiceCommandParser
{
    public static bool TryParse(string transcript, out PrototypeVoiceCommand command)
    {
        command = PrototypeVoiceCommand.None;
        string text = NormalizeText(transcript, true);
        foreach (string amount in new[] { "一點點", "一点点", "一小段", "一小步", "一點", "一点" })
        {
            if (!text.EndsWith(amount, StringComparison.Ordinal)) continue;
            if (!TryParseDirection(text.Substring(0, text.Length - amount.Length), out var direction)) return false;
            switch (direction)
            {
                case PrototypeVoiceCommand.Forward: command = PrototypeVoiceCommand.NudgeForward; break;
                case PrototypeVoiceCommand.Backward: command = PrototypeVoiceCommand.NudgeBackward; break;
                case PrototypeVoiceCommand.Left: command = PrototypeVoiceCommand.NudgeLeft; break;
                case PrototypeVoiceCommand.Right: command = PrototypeVoiceCommand.NudgeRight; break;
            }
            return command != PrototypeVoiceCommand.None;
        }
        switch (text)
        {
            case "掉頭": case "掉头": case "調頭": case "调头": case "迴轉": case "回轉": case "回转":
            case "轉半圈": case "转半圈": case "uturn": command = PrototypeVoiceCommand.UTurn; break;
        }
        if (command != PrototypeVoiceCommand.None) return true;
        return TryParseDirection(text, out command);
    }

    public static string NormalizeText(string transcript, bool preserveSmallAmount = false)
    {
        if (string.IsNullOrWhiteSpace(transcript)) return "";
        var normalized = new StringBuilder();
        foreach (char c in transcript.Normalize(NormalizationForm.FormKC))
        {
            if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c))
                normalized.Append(char.ToLowerInvariant(c));
        }
        string text = normalized.ToString();
        string[] prefixes = { "麻煩你", "麻煩", "請你", "請", "请你", "请", "幫我", "帮我" };
        string[] suffixes = preserveSmallAmount ? new[] { "謝謝", "谢谢", "一下", "吧" } :
            new[] { "謝謝", "谢谢", "一下", "一點", "一点", "吧" };
        for (int i = 0; i < 6; i++)
        {
            string previous = text;
            foreach (string prefix in prefixes)
                if (text.StartsWith(prefix, StringComparison.Ordinal))
                { text = text.Substring(prefix.Length); break; }
            foreach (string suffix in suffixes)
                if (text.EndsWith(suffix, StringComparison.Ordinal))
                { text = text.Substring(0, text.Length - suffix.Length); break; }
            if (text == previous) break;
        }
        return text;
    }

    private static bool TryParseDirection(string text, out PrototypeVoiceCommand command)
    {
        command = PrototypeVoiceCommand.None;
        switch (text)
        {
            case "前進": case "前进": case "往前": case "向前":
            case "往前走": case "向前走": case "往前移動": case "向前移動":
            case "forward": case "goforward": command = PrototypeVoiceCommand.Forward; break;
            case "後退": case "后退": case "往後": case "往后": case "向後": case "向后":
            case "往後走": case "往后走": case "向後走": case "往後移動":
            case "back": case "backward": case "goback": command = PrototypeVoiceCommand.Backward; break;
            case "左轉": case "左转": case "往左": case "向左": case "往左轉": case "向左轉":
            case "left": case "turnleft": command = PrototypeVoiceCommand.Left; break;
            case "右轉": case "右转": case "往右": case "向右": case "往右轉": case "向右轉":
            case "right": case "turnright": command = PrototypeVoiceCommand.Right; break;
            case "停止": case "停": case "停下": case "停下來": case "停下来":
            case "停車": case "停车": case "不要動": case "不要动": case "別動": case "别动":
            case "stop": command = PrototypeVoiceCommand.Stop; break;
        }
        return command != PrototypeVoiceCommand.None;
    }

    public static string GetLabel(PrototypeVoiceCommand command)
    {
        switch (command)
        {
            case PrototypeVoiceCommand.Forward: return "前進";
            case PrototypeVoiceCommand.Backward: return "後退";
            case PrototypeVoiceCommand.Left: return "左轉";
            case PrototypeVoiceCommand.Right: return "右轉";
            case PrototypeVoiceCommand.Stop: return "停止";
            case PrototypeVoiceCommand.UTurn: return "掉頭";
            case PrototypeVoiceCommand.NudgeForward: return "前進一點";
            case PrototypeVoiceCommand.NudgeBackward: return "後退一點";
            case PrototypeVoiceCommand.NudgeLeft: return "左轉一點";
            case PrototypeVoiceCommand.NudgeRight: return "右轉一點";
            default: return "未識別指令";
        }
    }

    public static bool IsNudge(PrototypeVoiceCommand command)
    {
        return command == PrototypeVoiceCommand.NudgeForward || command == PrototypeVoiceCommand.NudgeBackward ||
            command == PrototypeVoiceCommand.NudgeLeft || command == PrototypeVoiceCommand.NudgeRight;
    }
}
