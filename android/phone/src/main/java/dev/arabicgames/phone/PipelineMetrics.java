// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
import org.json.JSONObject;import java.util.concurrent.atomic.AtomicLong;
/** Counters only: no frames, API keys, URLs or network identifiers. */
final class PipelineMetrics {
 static final AtomicLong ocr=new AtomicLong(),filtered=new AtomicLong(),empty=new AtomicLong(),queued=new AtomicLong(),superseded=new AtomicLong(),expired=new AtomicLong(),failures=new AtomicLong(),ocrFailures=new AtomicLong(),published=new AtomicLong(),captureTimeouts=new AtomicLong();
 static JSONObject snapshot(){JSONObject o=new JSONObject();try{o.put("scope","since-process-start").put("capture_timeouts",captureTimeouts.get()).put("ocr",ocr.get()).put("filtered_ui",filtered.get()).put("empty_frames",empty.get()).put("local_queued",queued.get()).put("superseded",superseded.get()).put("expired",expired.get()).put("translation_failures",failures.get()).put("ocr_failures",ocrFailures.get()).put("published",published.get());}catch(Exception ignored){}return o;}
}
