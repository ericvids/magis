/************************************************************************************************************

MAGIS copyright © 2018, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

using UnityEngine;
using System.Collections;

public class QuaternionMovingAverage
{
    private const int NUM_SAMPLES = 16;

    private Quaternion[] samples;
    private int currentSample = -1;

    public QuaternionMovingAverage()
    {
        samples = new Quaternion[NUM_SAMPLES];
    }

    private Quaternion Average(int start, int size)
    {
        if (size == 1)
            return samples[start];
        else
            return Quaternion.Slerp(Average(start, size / 2), Average(start + size / 2, size / 2), 0.5f);
    }

    public Quaternion GetLastSample()
    {
        if (currentSample == -1)
            return Quaternion.identity;
        else
            return samples[currentSample];
    }

    public Quaternion GetAverage()
    {
        if (currentSample == -1)
            return Quaternion.identity;
        else
            return Average(0, NUM_SAMPLES);
    }

    public void AddSample(Quaternion q)
    {
        if (currentSample == -1)
        {
            for (int i = 0; i < NUM_SAMPLES; i++)
                samples[i] = new Quaternion(q.x, q.y, q.z, q.w);
            currentSample = 0;
        }
        else
        {
            currentSample = (currentSample + 1) % NUM_SAMPLES;
            samples[currentSample] = new Quaternion(q.x, q.y, q.z, q.w);
        }
    }

    public void Reset()
    {
        currentSample = -1;
    }
}
