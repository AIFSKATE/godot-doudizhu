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
            if (_sessionLandlord != null) return;

            string pathL = ProjectSettings.GlobalizePath("res://Baseline/landlord.onnx");
            string pathUp = ProjectSettings.GlobalizePath("res://Baseline/landlord_up.onnx");
            string pathDown = ProjectSettings.GlobalizePath("res://Baseline/landlord_down.onnx");

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

    public static int GetLandlordAction(float[] z, float[] x)
    {
        return RunInference(_sessionLandlord, z, x, 373);
    }

    public static int GetUpFarmerAction(float[] z, float[] x)
    {
        return RunInference(_sessionUp, z, x, 484);
    }

    public static int GetDownFarmerAction(float[] z, float[] x)
    {
        return RunInference(_sessionDown, z, x, 484);
    }

    private static int RunInference(InferenceSession session, float[] z, float[] x, int xDim)
    {
        if (session == null)
        {
            GD.PrintErr("Session 未初始化！");
            return 0;
        }

        // 动态推断传入了多少种合法的牌型动作 (BatchSize)
        int batchSize = x.Length / xDim;

        var inputs = new List<NamedOnnxValue>
    {
        NamedOnnxValue.CreateFromTensor("input_z", new DenseTensor<float>(z, new[] { batchSize, 5, 162 })),
        NamedOnnxValue.CreateFromTensor("input_x", new DenseTensor<float>(x, new[] { batchSize, xDim }))
    };

        // 运行推理，获取结果
        using var results = session.Run(inputs);
        var firstOutput = results.First();

        int bestIdx = 0; // 用于记录最高胜率的动作索引

        // 【情况 A】：模型输出的得分是 Int64 (long) 类型
        if (firstOutput.Value is Tensor<long> longTensor)
        {
            long[] scores = longTensor.ToArray(); // 拿到所有牌型的得分数组
            long maxScore = long.MinValue;

            // 遍历所有得分，手动执行 ArgMax 寻找最高概率/得分的动作
            for (int i = 0; i < scores.Length; i++)
            {
                if (scores[i] > maxScore)
                {
                    maxScore = scores[i];
                    bestIdx = i;
                }
            }
        }
        // 【情况 B】：模型输出的得分是 float 类型 (更常见的概率格式，做个兼容兜底)
        else if (firstOutput.Value is Tensor<float> floatTensor)
        {
            float[] scores = floatTensor.ToArray(); // 拿到所有牌型的胜率数组
            float maxScore = float.MinValue;

            for (int i = 0; i < scores.Length; i++)
            {
                if (scores[i] > maxScore)
                {
                    maxScore = scores[i];
                    bestIdx = i;
                }
            }
        }
        else
        {
            GD.PrintErr($"❌ 无法解析的模型输出类型: {firstOutput.Value?.GetType().FullName}");
            return 0; // 兜底返回第一个合法动作
        }

        // 返回能带来最高胜率的动作索引
        return bestIdx;
    }

    public static void Dispose()
    {
        _sessionLandlord?.Dispose();
        _sessionUp?.Dispose();
        _sessionDown?.Dispose();
        GD.Print("AI 引擎资源已释放");
    }
}