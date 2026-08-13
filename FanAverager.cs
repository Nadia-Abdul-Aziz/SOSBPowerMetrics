// rolling window of fan RPM samples: one row per poll cycle, one column per fan.
// to change the timeframe of the average, change the sample count passed in.
class FanAverager
{
    readonly float[,] _table;
    int _index;

    public FanAverager(int sampleCount, int fanCount)
    {
        _table = new float[sampleCount, fanCount];
    }

    public int FanCount => _table.GetLength(1);

    // a blank sample, NaN meaning "no sensor reported this column"
    public float[] NewSample()
    {
        float[] sample = new float[FanCount];
        for (int i = 0; i < sample.Length; i++)
            sample[i] = float.NaN;
        return sample;
    }

    // adds one sample. returns the column averages once the window fills, otherwise null.
    public float[] AddSample(float[] sample)
    {
        for (int i = 0; i < FanCount; i++)
            _table[_index, i] = i < sample.Length ? sample[i] : float.NaN;

        _index++;
        if (_index < _table.GetLength(0))
            return null;

        _index = 0;
        return Average();
    }

    float[] Average()
    {
        float[] total = new float[FanCount];
        int[] counts = new int[FanCount];

        for (int i = 0; i < _table.GetLength(0); i++)
        {
            for (int j = 0; j < FanCount; j++)
            {
                if (float.IsNaN(_table[i, j])) // sensor didn't report that sample, don't let it drag the mean down
                    continue;
                total[j] += _table[i, j];
                counts[j]++;
            }
        }

        for (int i = 0; i < total.Length; i++)
            total[i] = counts[i] > 0 ? total[i] / counts[i] : float.NaN;

        return total;
    }
}
