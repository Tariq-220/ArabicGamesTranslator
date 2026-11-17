// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import android.content.Context;
import android.hardware.Sensor;
import android.hardware.SensorManager;
import android.os.PowerManager;

/** Native proximity display blanking; never locks the device or grants background capture. */
final class ProximityDisplay {
 private final PowerManager power;private final boolean sensorPresent;private PowerManager.WakeLock lock;
 ProximityDisplay(Context context){power=(PowerManager)context.getSystemService(Context.POWER_SERVICE);SensorManager sensors=(SensorManager)context.getSystemService(Context.SENSOR_SERVICE);sensorPresent=sensors!=null&&sensors.getDefaultSensor(Sensor.TYPE_PROXIMITY)!=null;}
 boolean supported(){return sensorPresent&&power!=null&&power.isWakeLockLevelSupported(PowerManager.PROXIMITY_SCREEN_OFF_WAKE_LOCK);}
 boolean active(){return lock!=null&&lock.isHeld();}
 boolean start(){if(!supported())return false;if(active())return true;try{lock=power.newWakeLock(PowerManager.PROXIMITY_SCREEN_OFF_WAKE_LOCK,"ArabicGames:ProximityDisplay");lock.setReferenceCounted(false);lock.acquire();return true;}catch(RuntimeException error){stop();return false;}}
 void stop(){if(lock!=null){if(lock.isHeld())lock.release();lock=null;}}
}
