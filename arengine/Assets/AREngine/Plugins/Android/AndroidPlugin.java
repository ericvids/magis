/************************************************************************************************************

MAGIS copyright © 2015-2019, Ateneo de Manila University.

This program (excluding certain assets as indicated in arengine/Assets/ARGames/_SampleGame/Resources/Credits.txt) is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License v2 ONLY, as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License v2 along with this program; if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

************************************************************************************************************/

import android.content.Context;
import android.hardware.Sensor;
import android.hardware.SensorEvent;
import android.hardware.SensorEventListener;
import android.hardware.SensorManager;

/**
 * This class implements a software-based rotation sensor that derives its
 * sensor values from the accelerometer and magnetic field sensors rather than
 * relying on the hardware-exposed rotation vector sensor, because older phones
 * such as the HTC Incredible S have incorrect implementations for this sensor.
 *
 * This code will not be used if a gyroscope is available (since for that,
 * we can safely use Unity3D's implementation).
*/
public class AndroidPlugin implements SensorEventListener
{
    private Context context;
    private SensorManager sensorManager;
    private int sensorRate;
    private Sensor accelerometerSensor;
    private Sensor magneticFieldSensor;
    private float[] accelerometer;
    private float[] magneticField;
    private float[] m;
    private float[] q;

    public AndroidPlugin(Context context, int sensorRate)
    {
        this.context = context;
        this.sensorRate = sensorRate;
        sensorManager = (SensorManager) context.getSystemService(Context.SENSOR_SERVICE);
        accelerometerSensor = sensorManager.getDefaultSensor(Sensor.TYPE_ACCELEROMETER);
        magneticFieldSensor = sensorManager.getDefaultSensor(Sensor.TYPE_MAGNETIC_FIELD);
        accelerometer = new float[3];
        magneticField = new float[3];
        m = new float[9];
        q = new float[4];

        if (isRotationSensorAvailable())
        {
            sensorManager.registerListener(this, accelerometerSensor, sensorRate);
            sensorManager.registerListener(this, magneticFieldSensor, sensorRate);
        }
    }

    public void onSensorChanged(SensorEvent event)
    {
        if (event.sensor.getType() == Sensor.TYPE_ACCELEROMETER)
            System.arraycopy(event.values, 0, accelerometer, 0, 3);
        else if (event.sensor.getType() == Sensor.TYPE_MAGNETIC_FIELD)
            System.arraycopy(event.values, 0, magneticField, 0, 3);

        if (SensorManager.getRotationMatrix(m, null, accelerometer, magneticField))
        {
            q[0] = (float) Math.sqrt(Math.max(0, 1 + m[0] - m[4] - m[8])) / 2 * Math.signum(m[7] - m[5]);
            q[1] = (float) Math.sqrt(Math.max(0, 1 - m[0] + m[4] - m[8])) / 2 * Math.signum(m[2] - m[6]);
            q[2] = (float) Math.sqrt(Math.max(0, 1 - m[0] - m[4] + m[8])) / 2 * Math.signum(m[3] - m[1]);
            q[3] = (float) Math.sqrt(Math.max(0, 1 + m[0] + m[4] + m[8])) / 2;
        }
    }

    public void onAccuracyChanged(Sensor sensor, int accuracy)
    {
    }

    public void destroy()
    {
        sensorManager.unregisterListener(this);
    }

    public boolean isRotationSensorAvailable()
    {
        return accelerometerSensor != null && magneticFieldSensor != null;
    }

    public float getX()
    {
        return q[0];
    }

    public float getY()
    {
        return q[1];
    }

    public float getZ()
    {
        return q[2];
    }

    public float getW()
    {
        return q[3];
    }
}
