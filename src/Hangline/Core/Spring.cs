namespace Hangline.Core;

/// <summary>
/// A damped spring integrator that reproduces SwiftUI's two spring flavours:
/// <c>.spring(response:dampingFraction:)</c> and
/// <c>.interpolatingSpring(stiffness:damping:)</c> (mass 1).
/// All motion in Hangline is driven by this so breeze, arrival, press and
/// hover compose on the same card instead of fighting each other.
/// </summary>
public static class Spring
{
    /// SwiftUI derives stiffness and damping from response and dampingFraction:
    /// ω₀ = 2π/response, k = ω₀², c = 2ζω₀.
    public static (double Stiffness, double Damping) FromResponse(double response, double dampingFraction)
    {
        double w0 = 2 * Math.PI / Math.Max(1e-6, response);
        return (w0 * w0, 2 * dampingFraction * w0);
    }

    /// One semi-implicit Euler step toward <paramref name="target"/>.
    public static void Step(ref double value, ref double velocity, double target, double dt,
                            double stiffness, double damping, int substeps = 4)
    {
        if (dt <= 0) return;
        double h = dt / substeps;
        for (int i = 0; i < substeps; i++)
        {
            double accel = -stiffness * (value - target) - damping * velocity;
            velocity += accel * h;
            value += velocity * h;
        }
    }

    public static void StepResponse(ref double value, ref double velocity, double target, double dt,
                                    double response, double dampingFraction, int substeps = 4)
    {
        var (k, c) = FromResponse(response, dampingFraction);
        Step(ref value, ref velocity, target, dt, k, c, substeps);
    }

    /// Has the spring settled closely enough to stop ticking frames?
    public static bool IsSettled(double value, double velocity, double target,
                                 double epsilon = 0.002, double velocityEpsilon = 0.01)
        => Math.Abs(value - target) < epsilon && Math.Abs(velocity) < velocityEpsilon;

    /// Sample the whole curve; used by tests to check overshoot and settle time.
    public static List<double> Curve(double from, double to, double response, double dampingFraction,
                                     double duration, double frame = 1.0 / 120)
    {
        var points = new List<double>();
        double value = from, velocity = 0;
        for (double t = 0; t <= duration; t += frame)
        {
            StepResponse(ref value, ref velocity, to, frame, response, dampingFraction);
            points.Add(value);
        }
        return points;
    }
}
