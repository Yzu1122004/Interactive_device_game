using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Can also be run headlessly with -executeMethod PrototypeValidation.RunBatch.
public static class PrototypeValidation
{
    [MenuItem("Prototype/Validate Input and Scene")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before validation.");
        ValidateEncoder();
        ValidateVoice();
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/prototype.unity");
        bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
        if (!alreadyLoaded)
            scene = EditorSceneManager.OpenScene("Assets/Scenes/prototype.unity", OpenSceneMode.Additive);
        try
        {
            var components = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var serial = components.OfType<PrototypeArduinoConnection>().Single();
            var encoder = components.OfType<PrototypeEncoderInput>().Single();
            var controller = components.OfType<PrototypeWheelchairController>().Single();
            var voice = components.OfType<PrototypeVoiceControl>().Single();
            Check(serial.enabled && encoder.enabled && controller.enabled, "Prototype components enabled");
            Check(encoder.arduino == serial && controller.encoder == encoder, "Input references");
            Check(voice.enabled && voice.wheelchair == controller && voice.pushToTalkKey == KeyCode.V,
                "Voice target and V key");
            Check(voice.pushToTalkKey != controller.brakeKey, "Recording and brake keys distinct");
            Check(controller.wheel_left != null && controller.wheel_right != null &&
                controller.wheel_left_col != null && controller.wheel_right_col != null,
                "Prefab wheel references");
            Check(controller.GetComponent<Rigidbody>() != null, "Wheelchair Rigidbody");
            var body = controller.GetComponent<Rigidbody>();
            Check(!body.isKinematic && body.useGravity && body.detectCollisions,
                "Wheelchair dynamic gravity and collisions");
            Check(controller.CanDriveRoutePhysics, "Route wheel colliders stay enabled");
            Check(!components.OfType<ArduinoBasic>().Any(item => item.enabled), "Old serial reader inactive");
            Check(!components.OfType<WheelController>().Any(item => item.enabled), "Old movement inactive");
            Check(!components.OfType<PlayerCollision>().Any(item => item.enabled), "Old skills inactive");
            var network = components.OfType<PrototypeRouteNetwork>().Single();
            var navigator = components.OfType<PrototypeRouteNavigator>().Single();
            Check(navigator.network == network && navigator.wheelchair == controller &&
                voice.navigation == navigator && voice.useRouteNavigation, "Route navigation references");
            Check(network.Contains(network.startNode), "Route start node");
            foreach (var node in network.Nodes)
                Check(network.TryFindPath(network.startNode, node, out var path), "Route reachable: " + node.name);
            var bank = network.FindDestination("銀行");
            Check(bank != null && network.TryFindPath(network.startNode, bank, out var bankPath), "Bank destination reachable");
            Debug.Log("PROTOTYPE VALIDATION PASSED: encoder, voice parser, WAV, route network and scene references.");
        }
        finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
    }

    public static void RunBatch()
    {
        try { Validate(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static void ValidateEncoder()
    {
        var host = new GameObject("Encoder validation");
        host.SetActive(false); // No hardware connection during tests.
        try
        {
            host.AddComponent<PrototypeArduinoConnection>().connectOnEnable = false;
            var input = host.AddComponent<PrototypeEncoderInput>();
            input.invertLeftEncoder = false;
            Expect(input, "MOVE:2,2", 1f, 0f);
            Expect(input, "MOVE:-2,-2", -1f, 0f);
            Expect(input, "MOVE:-2,2", 0f, 1f);
            Expect(input, "MOVE:2,-2", 0f, -1f);
            Expect(input, "MOVE:2,0", 1f, -1f);
            Expect(input, "MOVE:0,2", 1f, 1f);
            Expect(input, "MOVE:0,0", 0f, 0f);
            Expect(input, "MOVE:1000,1000", 0f, 0f);
            Expect(input, "MOVE:NaN,2", 0f, 0f);
            Expect(input, "MOVE:1,2,3", 0f, 0f);
            Expect(input, "ATTACK_ON", 0f, 0f);
            input.invertLeftEncoder = true;
            Expect(input, "MOVE:-2,2", 1f, 0f);
            input.ResetInput();
            input.ProcessMessage("MOVE:123,456");
            Check(input.VerticalInput == 0f && input.HorizontalInput == 0f, "First packet baseline");
            input.ResetInput();
            Check(input.VerticalInput == 0f && input.HorizontalInput == 0f, "Reset clears movement");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void Expect(PrototypeEncoderInput input, string message, float forward, float turn)
    {
        input.ResetInput();
        input.ProcessMessage("MOVE:0,0");
        input.ProcessMessage(message);
        Check(Mathf.Approximately(input.VerticalInput, forward) &&
            Mathf.Approximately(input.HorizontalInput, turn), "Encoder case " + message);
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Validation failed: " + description);
    }

    private static void ValidateVoice()
    {
        string[] phrases = { "請幫我前進一下，謝謝。", "后退", "往左轉", "右轉。", "停下來",
            "前進一點", "后退一点", "請幫我左轉一點點，謝謝。", "右轉一點" };
        PrototypeVoiceCommand[] expected = { PrototypeVoiceCommand.Forward, PrototypeVoiceCommand.Backward,
            PrototypeVoiceCommand.Left, PrototypeVoiceCommand.Right, PrototypeVoiceCommand.Stop,
            PrototypeVoiceCommand.NudgeForward, PrototypeVoiceCommand.NudgeBackward,
            PrototypeVoiceCommand.NudgeLeft, PrototypeVoiceCommand.NudgeRight };
        for (int i = 0; i < phrases.Length; i++)
            Check(PrototypeVoiceCommandParser.TryParse(phrases[i], out var command) && command == expected[i], phrases[i]);
        foreach (string phrase in new[] { "不要前進", "不要停止", "前進後退", "前進十秒", "謝謝觀看",
            "不要前進一點", "前進一點然後右轉", "左轉十度", "", null })
            Check(!PrototypeVoiceCommandParser.TryParse(phrase, out var command), "Reject ambiguous voice input");
        Check(PrototypeVoiceCommandParser.TryParse("掉頭", out var uturn) &&
            uturn == PrototypeVoiceCommand.UTurn, "U-turn command");
        Check(PrototypeRouteGeometry.MatchesTurn(-90f, PrototypeVoiceCommand.Left) &&
            PrototypeRouteGeometry.MatchesTurn(90f, PrototypeVoiceCommand.Right), "Relative intersection direction");
        Check(!PrototypeRouteGeometry.MatchesTurn(180f, PrototypeVoiceCommand.Right), "U-turn is not right turn");

        byte[] wav = PrototypeVoiceAudio.EncodeWav(new float[] { 0.5f, 0.5f, -0.5f, -0.5f }, 2, 16000, out float rms);
        Check(wav.Length == 48 && System.Text.Encoding.ASCII.GetString(wav, 0, 4) == "RIFF", "WAV length and magic");
        Check(BitConverter.ToInt16(wav, 22) == 1 && BitConverter.ToInt32(wav, 24) == 16000, "WAV mono sample rate");
        Check(Mathf.Approximately(rms, 0.5f), "Audio RMS");
    }
}
