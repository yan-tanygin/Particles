using System.Numerics;

namespace CollidingParticles.Physics;

public sealed class SimulationSettings
{
    public float Restitution { get; set; } = 0.95f;
    public float BoundsPadding { get; set; } = 2f;
}

public sealed class Particle
{
    public Particle(Vector2 position, Vector2 velocity, float radius, float mass)
    {
        Position = position;
        Velocity = velocity;
        Radius = radius;
        Mass = mass;
    }

    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Radius { get; }
    public float Mass { get; }
}

public sealed class ParticleWorld
{
    private readonly List<Particle> _particles = new();

    public ParticleWorld(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public float Width { get; private set; }
    public float Height { get; private set; }
    public IReadOnlyList<Particle> Particles => _particles;
    public SimulationSettings Settings { get; } = new();

    public void Resize(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public void Clear() => _particles.Clear();

    public void Add(Particle particle) => _particles.Add(particle);

    public void Step(float dt)
    {
        if (dt <= 0f || _particles.Count == 0)
        {
            return;
        }

        for (int i = 0; i < _particles.Count; i++)
        {
            Particle particle = _particles[i];
            particle.Position += particle.Velocity * dt;
            ResolveBounds(particle);
        }

        for (int i = 0; i < _particles.Count; i++)
        {
            for (int j = i + 1; j < _particles.Count; j++)
            {
                ResolveCollision(_particles[i], _particles[j]);
            }
        }
    }

    private void ResolveBounds(Particle particle)
    {
        float left = particle.Radius + Settings.BoundsPadding;
        float right = Width - particle.Radius - Settings.BoundsPadding;
        float top = particle.Radius + Settings.BoundsPadding;
        float bottom = Height - particle.Radius - Settings.BoundsPadding;
        Vector2 position = particle.Position;
        Vector2 velocity = particle.Velocity;

        if (position.X < left)
        {
            position.X = left;
            velocity.X = -velocity.X * Settings.Restitution;
        }
        else if (position.X > right)
        {
            position.X = right;
            velocity.X = -velocity.X * Settings.Restitution;
        }

        if (position.Y < top)
        {
            position.Y = top;
            velocity.Y = -velocity.Y * Settings.Restitution;
        }
        else if (position.Y > bottom)
        {
            position.Y = bottom;
            velocity.Y = -velocity.Y * Settings.Restitution;
        }

        particle.Position = position;
        particle.Velocity = velocity;
    }

    private void ResolveCollision(Particle a, Particle b)
    {
        Vector2 delta = b.Position - a.Position;
        float distance = delta.Length();
        float minDistance = a.Radius + b.Radius;

        if (distance <= 0f)
        {
            distance = 0.0001f;
            delta = new Vector2(minDistance, 0f);
        }

        if (distance >= minDistance)
        {
            return;
        }

        Vector2 normal = delta / distance;
        Vector2 relativeVelocity = b.Velocity - a.Velocity;
        float velocityAlongNormal = Vector2.Dot(relativeVelocity, normal);

        if (velocityAlongNormal > 0f)
        {
            ApplyPositionalCorrection(a, b, normal, minDistance - distance);
            return;
        }

        float inverseMassA = 1f / a.Mass;
        float inverseMassB = 1f / b.Mass;

        float impulseMagnitude = -(1f + Settings.Restitution) * velocityAlongNormal;
        impulseMagnitude /= inverseMassA + inverseMassB;

        Vector2 impulse = impulseMagnitude * normal;
        a.Velocity -= impulse * inverseMassA;
        b.Velocity += impulse * inverseMassB;

        ApplyPositionalCorrection(a, b, normal, minDistance - distance);
    }

    private static void ApplyPositionalCorrection(Particle a, Particle b, Vector2 normal, float penetration)
    {
        const float percent = 0.8f;
        const float slop = 0.01f;
        float corrected = MathF.Max(penetration - slop, 0f) * percent;
        if (corrected <= 0f)
        {
            return;
        }

        float inverseMassA = 1f / a.Mass;
        float inverseMassB = 1f / b.Mass;
        float totalInverseMass = inverseMassA + inverseMassB;
        Vector2 correction = (corrected / totalInverseMass) * normal;

        a.Position -= correction * inverseMassA;
        b.Position += correction * inverseMassB;
    }
}
