using System.Diagnostics;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CollidingParticles.Physics;

namespace CollidingParticles.App;

public partial class MainWindow : Window
{
    private const int DefaultParticleCount = 40;
    private readonly Random _random = new();
    private readonly List<Ellipse> _visuals = new();
    private readonly Stopwatch _stopwatch = new();
    private ParticleWorld? _world;
    private TimeSpan _lastFrame;
    private bool _isRunning = true;
    private float _speed = 1f;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SimulationCanvas.SizeChanged += SimulationCanvas_OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeWorld();
        CreateScene(DefaultParticleCount);
        _stopwatch.Restart();
        _lastFrame = _stopwatch.Elapsed;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
    }

    private void InitializeWorld()
    {
        float width = (float)Math.Max(200, SimulationCanvas.ActualWidth);
        float height = (float)Math.Max(200, SimulationCanvas.ActualHeight);
        _world = new ParticleWorld(width, height)
        {
            Settings =
            {
                Restitution = (float)RestitutionSlider.Value,
                BoundsPadding = 4f
            }
        };
    }

    private void CreateScene(int count)
    {
        if (_world is null)
        {
            return;
        }

        _world.Clear();
        SimulationCanvas.Children.Clear();
        _visuals.Clear();
        _world.Resize((float)SimulationCanvas.ActualWidth, (float)SimulationCanvas.ActualHeight);

        for (int i = 0; i < count; i++)
        {
            Particle particle = CreateParticle();
            _world.Add(particle);
            Ellipse visual = CreateVisual(particle);
            SimulationCanvas.Children.Add(visual);
            _visuals.Add(visual);
        }

        UpdateStats();
    }

    private void AddParticles(int count)
    {
        if (_world is null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Particle particle = CreateParticle();
            _world.Add(particle);
            Ellipse visual = CreateVisual(particle);
            SimulationCanvas.Children.Add(visual);
            _visuals.Add(visual);
        }

        UpdateStats();
    }

    private Particle CreateParticle()
    {
        float radius = RandomRange(6f, 18f);
        float mass = radius * radius * 0.05f;
        float width = (float)Math.Max(200, SimulationCanvas.ActualWidth);
        float height = (float)Math.Max(200, SimulationCanvas.ActualHeight);

        Vector2 position = FindFreePosition(radius, width, height);
        Vector2 velocity = new(RandomRange(-120f, 120f), RandomRange(-120f, 120f));

        return new Particle(position, velocity, radius, mass);
    }

    private Vector2 FindFreePosition(float radius, float width, float height)
    {
        if (_world is null)
        {
            return Vector2.Zero;
        }

        const int attempts = 20;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector2 candidate = new(
                RandomRange(radius + 8f, width - radius - 8f),
                RandomRange(radius + 8f, height - radius - 8f));

            bool overlaps = false;
            foreach (Particle particle in _world.Particles)
            {
                float minDistance = particle.Radius + radius + 2f;
                if (Vector2.Distance(particle.Position, candidate) < minDistance)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                return candidate;
            }
        }

        return new Vector2(
            RandomRange(radius + 8f, width - radius - 8f),
            RandomRange(radius + 8f, height - radius - 8f));
    }

    private Ellipse CreateVisual(Particle particle)
    {
        Color baseColor = ColorFromHsv(RandomRange(25, 55), 0.6, 0.95);
        Color highlight = Color.FromArgb(220, baseColor.R, baseColor.G, baseColor.B);

        RadialGradientBrush brush = new()
        {
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(240, 255, 255, 255), 0),
                new GradientStop(highlight, 0.45),
                new GradientStop(baseColor, 1)
            }
        };

        Ellipse ellipse = new()
        {
            Width = particle.Radius * 2,
            Height = particle.Radius * 2,
            Fill = brush
        };

        return ellipse;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_world is null)
        {
            return;
        }

        if (!_isRunning)
        {
            _lastFrame = _stopwatch.Elapsed;
            return;
        }

        TimeSpan now = _stopwatch.Elapsed;
        float dt = (float)(now - _lastFrame).TotalSeconds;
        _lastFrame = now;

        if (dt <= 0f)
        {
            return;
        }

        dt = MathF.Min(dt, 0.033f) * _speed;
        _world.Step(dt);
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (_world is null)
        {
            return;
        }

        for (int i = 0; i < _world.Particles.Count; i++)
        {
            Particle particle = _world.Particles[i];
            Ellipse visual = _visuals[i];
            Canvas.SetLeft(visual, particle.Position.X - particle.Radius);
            Canvas.SetTop(visual, particle.Position.Y - particle.Radius);
        }
    }

    private void SimulationCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _world?.Resize((float)e.NewSize.Width, (float)e.NewSize.Height);
    }

    private void StartPause_Click(object sender, RoutedEventArgs e)
    {
        _isRunning = !_isRunning;
        StartPauseButton.Content = _isRunning ? "Pause" : "Start";
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        CreateScene(DefaultParticleCount);
        _isRunning = true;
        StartPauseButton.Content = "Pause";
    }

    private void Burst_Click(object sender, RoutedEventArgs e)
    {
        AddParticles(12);
    }

    private void SpeedSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _speed = (float)SpeedSlider.Value;
        UpdateStats();
    }

    private void RestitutionSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_world is null)
        {
            return;
        }

        _world.Settings.Restitution = (float)RestitutionSlider.Value;
        UpdateStats();
    }

    private void UpdateStats()
    {
        if (_world is null)
        {
            return;
        }

        StatsText.Text = $"Particles: {_world.Particles.Count}\nSpeed: {_speed:0.00}x\nRestitution: {_world.Settings.Restitution:0.00}";
    }

    private float RandomRange(float min, float max)
    {
        return (float)(_random.NextDouble() * (max - min) + min);
    }

    private static Color ColorFromHsv(double hue, double saturation, double value)
    {
        double chroma = value * saturation;
        double x = chroma * (1 - Math.Abs((hue / 60.0 % 2) - 1));
        double m = value - chroma;

        (double r1, double g1, double b1) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };

        byte r = (byte)Math.Round((r1 + m) * 255);
        byte g = (byte)Math.Round((g1 + m) * 255);
        byte b = (byte)Math.Round((b1 + m) * 255);

        return Color.FromRgb(r, g, b);
    }
}

