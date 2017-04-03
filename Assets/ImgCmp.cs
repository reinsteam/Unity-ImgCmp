using UnityEngine;
using UnityEngine.Rendering;

public class ImgCmp : MonoBehaviour
{
    const int kNumLuminanceSlices = 8;
    const int kNumLuminanceBlurs = kNumLuminanceSlices - 1;

    private int imgW = 0;
    private int imgH = 0;


    [Range(0.1f, 89.9f)]
    public float FieldOfView = 45.0f;

    public float NumOneDegreePixels = 0.0f;

    public float PixelsPerDegree = 0.0f;

    [Range(0.0f, 1.0f)]
    public float ColorFactor = 1.0f;

    public int AdaptationLevel = 0;

    public float[] cpd = new float[kNumLuminanceSlices];
    public float[] frq = new float[kNumLuminanceSlices];

    public Texture2D ImageA;
    public Texture2D ImageB;
    public ComputeShader CS;

    public RenderTexture EDelta;

    public RenderTexture LumAB;

    public RenderTexture Result;

    void Start ()
    {
        if (ImageA != null && ImageB != null && ImageA.width == ImageB.width && ImageA.height == ImageB.height)
        {
            imgW = ImageA.width;
            imgH = ImageA.height;

            EDelta = new RenderTexture(imgW, imgH, 0, RenderTextureFormat.RFloat);
            EDelta.enableRandomWrite = true;
            EDelta.useMipMap = false;
            EDelta.autoGenerateMips = false;
            EDelta.Create();


            LumAB = new RenderTexture(imgW, imgH, 0, RenderTextureFormat.RFloat);
            LumAB.dimension = TextureDimension.Tex2DArray;
            LumAB.enableRandomWrite = true;
            LumAB.volumeDepth = kNumLuminanceSlices;
            LumAB.autoGenerateMips = false;
            LumAB.useMipMap = false;
            LumAB.Create();

            Result = new RenderTexture(imgW, imgH, 0, RenderTextureFormat.R8);
            Result.enableRandomWrite = true;
            Result.useMipMap = false;
            Result.autoGenerateMips = false;
            Result.Create();
        }
    }

    static private float csf(float cpd, float lum)
    {
        float a = 440.0f * Mathf.Pow((1.0f + 0.7f / lum), -0.2f);
        float b = 0.3f * Mathf.Pow((1.0f + 100.0f / lum), 0.15f);

        float exp_b_cpd = Mathf.Exp(b * cpd);

        return a * cpd * Mathf.Sqrt(1.0f + 0.06f * exp_b_cpd) / exp_b_cpd;
    }

    void Update ()
    {
        if (imgW > 0 && imgH > 0)
        {
            NumOneDegreePixels = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * 2.0f * Mathf.Rad2Deg;

            PixelsPerDegree = imgW / NumOneDegreePixels;

            cpd[0] = 0.5f * PixelsPerDegree;
            for (uint i = 1; i < kNumLuminanceSlices; ++i)
            {
                cpd[i] = 0.5f * cpd[i - 1];
            }

            float csf_max = csf(3.248f, 100.0f);

            for (uint i = 0; i < kNumLuminanceSlices - 2; ++i)
            {
                frq[i] = csf_max / csf(cpd[i], 100.0f);
            }

            float num_pixels = 1.0f;
            for (int i = 0; i < kNumLuminanceSlices; ++i)
            {
                AdaptationLevel = i;
                if (num_pixels > NumOneDegreePixels)
                {
                    break;
                }
                num_pixels *= 2.0f;
            }
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if ((ImageA != null) &&
            (ImageB != null) &&
            (ImageA.width  == ImageB.width) &&
            (ImageA.height == ImageB.height) &&
            (imgW == ImageA.width) &&
            (imgH == ImageA.height))
        {
            int numThreadGroupsX = (imgW + 7) >> 3;
            int numThreadGroupsY = (imgH + 7) >> 3;

            float [] TextureSizeInverted = new float[2];
            int[] TextureSizeUint = new int[2];

            TextureSizeInverted[0] = 1.0f / (float)imgW;
            TextureSizeInverted[1] = 1.0f / (float)imgH;

            TextureSizeUint[0] = imgW;
            TextureSizeUint[1] = imgH;

            CS.SetFloats("cbTextureSizeInverted", TextureSizeInverted);

            CS.SetFloats("cbCpdConsts", cpd);
            CS.SetFloats("cbFrqConsts", frq);
            CS.SetFloat("cbColorFactor", ColorFactor);

            CS.SetInts("cbTextureSizeUint", TextureSizeUint);
            CS.SetInt("cbAdaptationLevel", AdaptationLevel);

            CS.SetTexture(0, "ImageA", ImageA);
            CS.SetTexture(0, "ImageB", ImageB);
            CS.SetTexture(0, "EDeltaOut", EDelta);
            CS.SetTexture(0, "LumBlurredABOut", LumAB);
            CS.Dispatch(0, numThreadGroupsX, numThreadGroupsY, 1);

            CS.SetTexture(1, "LumBlurredABOut", LumAB);

            for (int i = 0; i < kNumLuminanceBlurs; ++i)
            {
                CS.SetInt("cbBlurSliceId", i);
                CS.Dispatch(1, numThreadGroupsX, numThreadGroupsY, 1);
            }

            CS.SetTexture(2, "EDeltaIn", EDelta);
            CS.SetTexture(2, "LumBlurredABIn", LumAB);

            CS.SetTexture(2, "Result", Result);
            CS.Dispatch(2, numThreadGroupsX, numThreadGroupsY, 1);

            Graphics.Blit(Result, dst);
        }
        else
        {
            Graphics.Blit(src, dst);
        }
    }
}
