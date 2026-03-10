namespace CollidingParticles.Physics;

public sealed class SimulationSettings
{
    public float Restitution { get; set; } = 0.95f;
    public float BoundsPadding { get; set; } = 2f;
}
