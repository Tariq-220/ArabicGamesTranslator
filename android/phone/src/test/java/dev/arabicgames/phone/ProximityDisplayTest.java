package dev.arabicgames.phone;
import android.content.Context;
import android.hardware.Sensor;
import android.hardware.SensorManager;
import android.os.PowerManager;
import org.junit.Test;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.Config;
import org.robolectric.shadows.ShadowSensor;
import static org.junit.Assert.*;
@RunWith(RobolectricTestRunner.class) @Config(sdk=35)
public class ProximityDisplayTest {
 @Test public void unsupportedHardwareDoesNotAcquireLock(){Context c=RuntimeEnvironment.getApplication();ProximityDisplay p=new ProximityDisplay(c);assertFalse(p.start());assertFalse(p.active());p.stop();}
 @Test public void nativeDisplayLockIsReleasedAndCanRestart(){
  Context c=RuntimeEnvironment.getApplication();SensorManager sensors=(SensorManager)c.getSystemService(Context.SENSOR_SERVICE);
  Shadows.shadowOf(sensors).addSensor(ShadowSensor.newInstance(Sensor.TYPE_PROXIMITY));
  PowerManager power=(PowerManager)c.getSystemService(Context.POWER_SERVICE);Shadows.shadowOf(power).setIsWakeLockLevelSupported(PowerManager.PROXIMITY_SCREEN_OFF_WAKE_LOCK,true);
  ProximityDisplay p=new ProximityDisplay(c);assertTrue(p.supported());assertTrue(p.start());assertTrue(p.active());assertTrue(p.start());p.stop();assertFalse(p.active());p.stop();assertTrue(p.start());p.stop();assertFalse(p.active());
 }
}
