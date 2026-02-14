namespace LeoQ.Core.Random;

public sealed class DeterministicRandom
{
    private readonly System.Random _rng;


    public DeterministicRandom(int seed) => _rng = new System.Random(seed);
    public double NextUniform() => _rng.NextDouble();

    public double NextGaussian(double mean, double stdDev)
    {
        double u1 = 1.0 - _rng.NextDouble();
        double u2 = 1.0 - _rng.NextDouble();
        double randStdNormal = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) *
                               System.Math.Sin(2.0 * System.Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }
}
