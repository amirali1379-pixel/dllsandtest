# Sentry Gaze Capture & Analysis

## 1. Project Overview

Sentry Gaze Capture & Analysis is an offline gaze-data acquisition and analysis project built around the Tobii Game Integration API.

The project is designed to capture raw eye-gaze coordinates and timestamps from a connected Tobii-compatible eye tracker, store the raw measurements, validate the quality of the recorded signal, and perform offline analysis.

The project is **not an AI system**, **not an eye-tracking game**, and **not dependent on the final application that will consume the data**.

Its main purpose is to create a reliable and reusable gaze-data pipeline.

---

# 2. Main Objective

The project has five primary goals:

1. Capture gaze data reliably.
2. Preserve the original raw data.
3. Measure the quality and timing of the gaze stream.
4. Analyze gaze movement characteristics.
5. Produce reusable data that another project can consume later.

The important architectural principle is:

```text
Eye Tracker
    ↓
Gaze Capture
    ↓
Raw Gaze Data
    ↓
Quality Analysis
    ↓
Signal Analysis
    ↓
Processed Gaze Data
    ↓
Other Applications
```

The capture layer should remain independent from future applications.

---

# 3. Current Hardware / API Layer

The current implementation communicates with the eye tracker through:

```text
Tobii.GameIntegration.Net
```

The tracker is accessed through the Game Integration API.

The current tracker URL used during testing is:

```text
tet-tcp://127.0.0.1
```

The application initializes the API and tracker connection and then retrieves gaze samples.

---

# 4. Raw Gaze Data

Each captured gaze sample contains at minimum:

```text
TimestampMicroSeconds
X
Y
```

Example:

```text
18518739032,-0.780994,-0.484650
18518768976,-0.781866,-0.485543
18518798579,-0.782899,-0.484113
```

## 4.1 Timestamp

`TimestampMicroSeconds` represents the timestamp associated with the gaze measurement.

It is essential for:

- calculating sample intervals
- measuring latency
- calculating movement duration
- detecting timing gaps
- calculating velocity
- calculating acceleration
- synchronizing gaze with other sensor data

The timestamp must therefore be preserved exactly in the raw dataset.

---

# 5. Gaze Coordinates

The current data contains:

```text
X
Y
```

These represent the gaze position reported by the tracker.

The coordinate system must be treated as tracker-provided data until its exact normalization and coordinate convention have been formally validated.

No irreversible coordinate conversion should be applied to the raw data.

A future processing layer may convert coordinates into:

```text
Normalized coordinates
Screen pixels
Degrees of visual angle
Physical screen coordinates
```

depending on the calibration information available.

---

# 6. Raw CSV Storage

The capture program stores measurements in CSV files.

Current format:

```text
TimestampMicroSeconds,X,Y
```

Example:

```text
TimestampMicroSeconds,X,Y
19069605216,-0.754600,-0.490300
19069633430,-0.754700,-0.490400
19069650183,-0.806200,-0.391500
```

Raw CSV files are considered the original source data.

They must not be overwritten by processed results.

---

# 7. Why Raw Data Must Be Preserved

The raw dataset is the most important asset of the project.

If an analysis algorithm later turns out to be incorrect, the raw data allows the entire analysis pipeline to be rerun.

Therefore:

```text
Raw Data
    ↓
Processing A
    ↓
Results A

Raw Data
    ↓
Processing B
    ↓
Results B
```

is preferred over:

```text
Raw Data
    ↓
Modified Data
    ↓
Modified Again
```

The second approach permanently destroys information.

---

# 8. Data Quality Analysis

The Analyzer evaluates the timing and integrity of the gaze stream.

Important measurements include:

- number of samples
- recording duration
- approximate sample rate
- average sample interval
- minimum sample interval
- maximum sample interval
- large timing gaps
- coordinate range
- abnormal movements

---

# 9. Sample Rate

Sample rate is estimated from:

```text
Number of intervals
-------------------
Recording duration
```

For example, one captured session produced:

```text
Samples:       156
Duration:      5.580 sec
Raw rate:      27.78 Hz
```

This does not automatically mean that the eye tracker hardware has a native 27.78 Hz sampling rate.

It only describes the effective rate observed in the captured dataset.

The distinction is important.

---

# 10. Timing Gaps

The analyzer detects unusually large gaps between consecutive samples.

A gap larger than the configured threshold is treated as a potentially unreliable interval.

Current analysis uses:

```text
Gap threshold = 100 ms
```

Example:

```text
1349.87 ms
945.11 ms
404.96 ms
```

These intervals are excluded from normal movement calculations.

---

# 11. Why Gap Filtering Exists

Suppose:

```text
Sample A
X=-0.74
Y=-1.24

        945 ms

Sample B
X=-0.06
Y=-0.26
```

If the system treats this as continuous eye movement, it may incorrectly calculate a huge movement and velocity.

The actual reason could be:

- tracking interruption
- application scheduling
- API behavior
- dropped data
- tracker communication delay
- system delay

Therefore the analyzer separates:

```text
Valid movement intervals
```

from:

```text
Large timing gaps
```

This prevents obvious data-quality problems from contaminating movement metrics.

---

# 12. Movement Distance

For two consecutive valid samples:

```text
ΔX = X₂ - X₁
ΔY = Y₂ - Y₁
```

Distance is calculated as:

```text
Distance = sqrt(ΔX² + ΔY²)
```

This represents the magnitude of the gaze movement between two samples in the current coordinate space.

---

# 13. Movement Velocity

Velocity is estimated using:

```text
Velocity = Distance / Time
```

The current implementation converts the interval into seconds before calculating velocity.

This produces a velocity value in coordinate-units per second.

Important:

The current velocity is **not yet a physical eye angular velocity**.

To obtain a scientifically meaningful visual-angle velocity, the system would need additional information such as:

- display dimensions
- viewing distance
- coordinate mapping
- tracker calibration

---

# 14. Velocity Distribution

The analyzer calculates velocity percentiles:

```text
P50
P90
P95
P99
```

Example from the current dataset:

```text
P50 = 0.446577
P90 = 6.013611
P95 = 8.282925
P99 = 13.766414
```

These values can later be useful for:

- detecting unusually fast gaze movements
- identifying movement populations
- comparing sessions
- detecting changes in behavior
- building movement classifiers

They should not yet be interpreted as universal human thresholds.

---

# 15. Largest Valid Movement

The analyzer identifies the largest movement that occurs inside a valid timing interval.

Example:

```text
Distance = 0.430994
ΔX       = -0.004731
ΔY       = -0.430968
Δt       = 45 ms
Velocity = 9.578069
```

This is useful for identifying large gaze movements while avoiding movements that occur across large data gaps.

---

# 16. Maximum Velocity

The analyzer also identifies the valid interval with the highest estimated velocity.

Example:

```text
Velocity = 13.828196
Distance = 0.392513
Δt      = 28.39 ms
```

This can later become useful for detecting fast gaze transitions.

However, maximum velocity should always be interpreted together with data quality.

A single extreme value is not enough to establish a physiological characteristic.

---

# 17. Future Signal Processing

The current project establishes the raw measurement and basic analysis layers.

A future processing layer can add:

```text
Raw Gaze
    ↓
Validity Check
    ↓
Gap Detection
    ↓
Noise Analysis
    ↓
Optional Filtering
    ↓
Clean Gaze Signal
```

Possible processing techniques include:

- smoothing
- low-pass filtering
- interpolation where scientifically justified
- outlier detection
- signal segmentation

Raw data must remain unchanged.

---

# 18. Fixation Detection

A future version can identify periods where gaze remains relatively stable.

A fixation generally represents a period of relatively stable gaze position.

Potential fixation measurements:

```text
Start time
End time
Duration
Mean X
Mean Y
Dispersion
```

These measurements can later be used for:

- reading analysis
- attention analysis
- target inspection
- visual search
- UI interaction analysis

---

# 19. Saccade / Rapid Movement Detection

A future processing layer can identify rapid gaze movements.

Potential measurements:

```text
Start
End
Duration
Amplitude
Direction
Peak velocity
Average velocity
```

These measurements can help characterize gaze movement behavior.

Again, thresholds should be validated against the tracker characteristics and the intended scientific use rather than blindly copied from another system.

---

# 20. Gaze Events

Eventually the project can represent gaze behavior as events:

```text
FIXATION
SACCADE
TRANSITION
TRACKING_LOSS
UNKNOWN
```

This allows raw samples to be transformed into higher-level behavioral information.

Example:

```text
Raw Samples
     ↓
Event Detection
     ↓
Fixation
     ↓
Saccade
     ↓
Fixation
```

---

# 21. Session Model

A future session can be represented conceptually as:

```text
Session
│
├── Session ID
├── Start Time
├── End Time
├── Tracker Information
├── Configuration
│
├── Raw Samples
│
├── Quality Metrics
│
├── Processed Samples
│
├── Fixations
│
├── Saccades
│
└── Summary Metrics
```

This makes the data much easier for another application to consume.

---

# 22. Visualization

A future visualization layer can display:

### Gaze Path

Shows the movement of gaze over time.

### Velocity Graph

Shows how gaze speed changes.

### Timing Graph

Shows the spacing between samples.

### Fixation Map

Shows where gaze remained stable.

### Heatmap

Shows where gaze was concentrated.

### Quality Timeline

Shows periods of:

```text
Good data
Gap
Tracking loss
Abnormal timing
```

Visualization is primarily a diagnostic and analysis tool.

It should not modify the raw data.

---

# 23. Export Formats

The project can eventually produce multiple outputs.

## Raw CSV

Best for:

- preservation
- simple inspection
- external analysis

## Processed CSV

Best for:

- statistical analysis
- spreadsheets
- Python/R processing

## JSON

Best for:

- application integration
- structured data
- session-based data

## Summary Report

Best for:

- human-readable results
- comparing sessions
- reporting quality

---

# 24. Standard Data Interface

One of the most important architectural goals is to make the project independent from the final application.

The final application should ideally consume a generic gaze representation such as:

```text
GazeSample
    Timestamp
    X
    Y
    Valid
```

This means another application does not need to understand the Tobii API.

Conceptually:

```text
Tobii
   ↓
Sentry Capture Adapter
   ↓
Standard GazeSample
   ↓
Consumer Application
```

This makes the data reusable.

---

# 25. What Another Project Can Get From Sentry

A future project can potentially use:

### Raw gaze position

```text
X
Y
Timestamp
```

Useful for:

- gaze visualization
- synchronization
- replay
- target tracking

### Gaze velocity

Useful for:

- movement classification
- detecting rapid transitions
- behavioral analysis

### Gaze acceleration

Useful for:

- movement dynamics
- detecting changes in movement

### Fixations

Useful for:

- attention analysis
- target inspection
- UI interaction

### Saccades

Useful for:

- visual search
- movement behavior
- reaction analysis

### Timing

Useful for:

- reaction-time analysis
- synchronization with mouse/keyboard
- event correlation

### Quality metrics

Useful for:

- rejecting bad trials
- identifying unreliable recordings
- comparing hardware/session quality

---

# 26. Combining Gaze With Other Sensors

The project is particularly useful because gaze data is timestamped.

This makes synchronization with another data source possible.

Conceptually:

```text
Time
│
├── Gaze
├── Mouse
├── Keyboard
├── Target Events
└── Application Events
```

For example:

```text
Gaze timestamp
        +
Mouse timestamp
        +
Target appearance timestamp
```

can be used to study relationships between visual attention and physical interaction.

---

# 27. Possible Future Applications

The data produced by this project can potentially be used in:

- eye-tracking research
- human-computer interaction
- UI/UX studies
- visual search experiments
- attention studies
- gaze-controlled interfaces
- accessibility systems
- reaction-time experiments
- game research
- human movement analysis
- sensor synchronization
- machine-learning datasets

These are possible future uses, not claims that the current project already implements them.

---

# 28. Important Scientific Limitations

The current data should not automatically be interpreted as:

- exact eye rotation angle
- exact visual angle
- physiological eye velocity
- medically meaningful measurements
- universal human behavior thresholds

Those interpretations require additional validation and calibration.

The current project should therefore be considered a:

**Gaze Data Acquisition and Analysis Platform**

rather than a medical or clinical measurement system.

---

# 29. Current Project Status

Current capabilities:

```text
[COMPLETE] Tobii/Game Integration connection
[COMPLETE] Gaze capture
[COMPLETE] Timestamp capture
[COMPLETE] CSV storage
[COMPLETE] Basic sample analysis
[COMPLETE] Timing analysis
[COMPLETE] Gap detection
[COMPLETE] Gap-aware movement analysis
[COMPLETE] Velocity calculation
[COMPLETE] Velocity percentiles
```

Next planned capabilities:

```text
[NEXT] API sampling-path validation
[NEXT] Accurate sampling characterization
[NEXT] Coordinate-system validation
[NEXT] Advanced signal quality analysis
[NEXT] Signal processing
[NEXT] Fixation detection
[NEXT] Saccade detection
[NEXT] Gaze-event model
[NEXT] Session model
[NEXT] Visualization
[NEXT] Standard export
[NEXT] Final documentation
```

---

# 30. Design Rules

The project follows these rules:

1. Raw data is never overwritten.
2. Processing is performed on copies or derived datasets.
3. Tracker-specific code remains isolated from analysis code.
4. Analysis should operate on standardized gaze data whenever possible.
5. No API behavior is assumed without verification.
6. Every processing stage should be testable independently.
7. Timing information must be preserved.
8. Data-quality problems must be identified rather than silently hidden.
9. Derived metrics must retain enough information to trace them back to raw data.
10. Future applications should consume the data through a stable interface rather than directly depending on the Tobii API.

---

# 31. Final Architecture

The intended architecture is:

```text
                 ┌─────────────────────┐
                 │   Tobii Eye Tracker │
                 └──────────┬──────────┘
                            │
                            ▼
                 ┌─────────────────────┐
                 │   Capture Layer     │
                 │ Tobii API Adapter   │
                 └──────────┬──────────┘
                            │
                            ▼
                 ┌─────────────────────┐
                 │    Raw Gaze Data    │
                 │       CSV           │
                 └──────────┬──────────┘
                            │
                            ▼
                 ┌─────────────────────┐
                 │   Quality Layer     │
                 │ Timing / Gaps / QA  │
                 └──────────┬──────────┘
                            │
                            ▼
                 ┌─────────────────────┐
                 │ Processing Layer    │
                 │ Filter / Velocity   │
                 │ Events / Fixations  │
                 └──────────┬──────────┘
                            │
                            ▼
                 ┌─────────────────────┐
                 │ Standard Gaze Data  │
                 └──────────┬──────────┘
                            │
                 ┌──────────┴──────────┐
                 ▼                     ▼
        ┌────────────────┐    ┌────────────────┐
        │ Visualization  │    │ Future Project │
        └────────────────┘    └────────────────┘
```

---

# 32. The Main Value of This Project

The most important output of this project is not the current console application.

The real value is the **validated gaze-data pipeline**.

The pipeline separates:

```text
Hardware
    ↓
Acquisition
    ↓
Raw Data
    ↓
Validation
    ↓
Processing
    ↓
Standardized Data
```

Once this pipeline is stable, future software does not need to rebuild the eye-tracking infrastructure.

It can simply consume the resulting gaze data.

That is why this project should be completed and kept as an independent component.