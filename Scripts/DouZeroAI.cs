using Godot;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;

public static class DouZeroAI
{
    private static InferenceSession _sessionLandlord;
    private static InferenceSession _sessionUp;
    private static InferenceSession _sessionDown;

    // 初始化所有模型
    public static void Initialize()
    {
        try
        {
            // 确保只初始化一次
            if (_sessionLandlord != null) return;

            // 获取绝对路径
            string pathL = ProjectSettings.GlobalizePath("res://Baseline/landlord.onnx");
            string pathUp = ProjectSettings.GlobalizePath("res://Baseline/landlord_up.onnx");
            string pathDown = ProjectSettings.GlobalizePath("res://Baseline/landlord_down.onnx");

            // 加载三个会话
            _sessionLandlord = new InferenceSession(pathL);
            _sessionUp = new InferenceSession(pathUp);
            _sessionDown = new InferenceSession(pathDown);

            GD.Print("✅ DouZero 三角色 AI 模型已全部加载成功！");
        }
        catch (Exception e)
        {
            GD.PrintErr($"❌ AI 初始化失败: {e.Message}");
        }
    }

    // 地主推理入口 (373维)
    public static int GetLandlordAction(float[] z, float[] x)
    {
        return RunInference(_sessionLandlord, z, x, 373);
    }

    // 农民上家推理入口 (484维)
    public static int GetUpFarmerAction(float[] z, float[] x)
    {
        return RunInference(_sessionUp, z, x, 484);
    }

    // 农民下家推理入口 (484维)
    public static int GetDownFarmerAction(float[] z, float[] x)
    {
        return RunInference(_sessionDown, z, x, 484);
    }

    // 通用的核心推理逻辑
    private static int RunInference(InferenceSession session, float[] z, float[] x, int xDim)
    {
        if (session == null)
        {
            GD.PrintErr("Session 未初始化！");
            return -1;
        }

        // 构造 Tensor
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_z", new DenseTensor<float>(z, new[] { 1, 5, 162 })),
            NamedOnnxValue.CreateFromTensor("input_x", new DenseTensor<float>(x, new[] { 1, xDim }))
        };

        // 运行并获取 Argmax
        using var results = session.Run(inputs);
        var output = results.First().AsEnumerable<float>().ToArray();

        // 找到分值最高的动作索引
        return Array.IndexOf(output, output.Max());
    }

    // 释放资源
    public static void Dispose()
    {
        _sessionLandlord?.Dispose();
        _sessionUp?.Dispose();
        _sessionDown?.Dispose();
        GD.Print("AI 引擎资源已释放");
    }
}