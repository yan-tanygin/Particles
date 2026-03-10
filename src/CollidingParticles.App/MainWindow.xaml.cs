using System.Diagnostics;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CollidingParticles.Physics;

namespace CollidingParticles.App;

public partial class MainWindow : Window
{
    private const int DefaultParticleCount = 40;
    private const float MinimumDepth = 320f;
    private readonly Random _random = new();
    private readonly List<TranslateTransform3D> _transforms = new();
    private readonly Stopwatch _stopwatch = new();
    private ParticleWorld? _world;
    private Model3DGroup? _scene;
    private Model3DGroup? _guideGroup;
    private TimeSpan _lastFrame;
    private bool _isRunning = true;
    private float _speed = 1f;
    private float _depth = 600f;
    private bool _isDragging;
    private Point _lastMouse;
    private double _cameraYaw = 0.6;
    private double _cameraPitch = -0.25;
    private double _cameraDistance;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SimulationViewport.SizeChanged += SimulationViewport_OnSizeChanged;
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
        float width = (float)Math.Max(200, SimulationViewport.ActualWidth);
        float height = (float)Math.Max(200, SimulationViewport.ActualHeight);
        _depth = Math.Max(MinimumDepth, width * 0.65f);
        _world = new ParticleWorld(width, height, _depth)
        {
            Settings =
            {
                Restitution = (float)RestitutionSlider.Value,
                BoundsPadding = 4f
            }
        };
        UpdateCamera();
    }

    private void BuildScene()
    {
        _scene = new Model3DGroup();
        _scene.Children.Add(new AmbientLight(Color.FromRgb(80, 80, 90)));
        _scene.Children.Add(new DirectionalLight(Color.FromRgb(220, 220, 210), new Vector3D(-0.6, -0.8, -1)));
        _scene.Children.Add(new DirectionalLight(Color.FromRgb(150, 150, 180), new Vector3D(0.6, 0.4, -0.3)));
        _guideGroup = BuildGuides();
        _scene.Children.Add(_guideGroup);
        SceneVisual.Content = _scene;
    }

    private void CreateScene(int count)
    {
        if (_world is null)
        {
            return;
        }

        _world.Clear();
        _transforms.Clear();
        BuildScene();
        _world.Resize((float)SimulationViewport.ActualWidth, (float)SimulationViewport.ActualHeight, _depth);

        for (int i = 0; i < count; i++)
        {
            Particle particle = CreateParticle();
            _world.Add(particle);
            GeometryModel3D visual = CreateVisual(particle);
            _scene?.Children.Add(visual);
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
            GeometryModel3D visual = CreateVisual(particle);
            _scene?.Children.Add(visual);
        }

        UpdateStats();
    }

    private Particle CreateParticle()
    {
        float radius = RandomRange(8f, 18f);
        float mass = radius * radius * 0.05f;
        float width = (float)Math.Max(200, SimulationViewport.ActualWidth);
        float height = (float)Math.Max(200, SimulationViewport.ActualHeight);

        Vector3 position = FindFreePosition(radius, width, height, _depth);
        Vector3 velocity = new(
            RandomRange(-120f, 120f),
            RandomRange(-120f, 120f),
            RandomRange(-120f, 120f));

        return new Particle(position, velocity, radius, mass);
    }

    private Vector3 FindFreePosition(float radius, float width, float height, float depth)
    {
        if (_world is null)
        {
            return Vector3.Zero;
        }

        const int attempts = 24;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector3 candidate = new(
                RandomRange(radius + 8f, width - radius - 8f),
                RandomRange(radius + 8f, height - radius - 8f),
                RandomRange(radius + 8f, depth - radius - 8f));

            bool overlaps = false;
            foreach (Particle particle in _world.Particles)
            {
                float minDistance = particle.Radius + radius + 2f;
                if (Vector3.Distance(particle.Position, candidate) < minDistance)
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

        return new Vector3(
            RandomRange(radius + 8f, width - radius - 8f),
            RandomRange(radius + 8f, height - radius - 8f),
            RandomRange(radius + 8f, depth - radius - 8f));
    }

    private GeometryModel3D CreateVisual(Particle particle)
    {
        Color baseColor = ColorFromHsv(RandomRange(25, 55), 0.55, 0.95);
        SolidColorBrush brush = new(baseColor);
        DiffuseMaterial material = new(brush);

        MeshGeometry3D mesh = CreateSphereMesh(particle.Radius, 16, 24);
        GeometryModel3D model = new()
        {
            Geometry = mesh,
            Material = material,
            BackMaterial = material
        };

        TranslateTransform3D translate = new();
        model.Transform = translate;
        _transforms.Add(translate);

        return model;
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
            TranslateTransform3D transform = _transforms[i];
            Vector3D scenePosition = WorldToScene(particle.Position);
            transform.OffsetX = scenePosition.X;
            transform.OffsetY = scenePosition.Y;
            transform.OffsetZ = scenePosition.Z;
        }
    }

    private Vector3D WorldToScene(Vector3 position)
    {
        if (_world is null)
        {
            return new Vector3D();
        }

        double x = position.X - _world.Width * 0.5f;
        double y = _world.Height * 0.5f - position.Y;
        double z = position.Z - _world.Depth * 0.5f;
        return new Vector3D(x, y, z);
    }

    private void SimulationViewport_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_world is null)
        {
            return;
        }

        float width = (float)Math.Max(200, e.NewSize.Width);
        float height = (float)Math.Max(200, e.NewSize.Height);
        _depth = Math.Max(MinimumDepth, width * 0.65f);
        _world.Resize(width, height, _depth);
        UpdateCamera();
        RebuildGuides();
        UpdateStats();
    }

    private void UpdateCamera()
    {
        if (_world is null)
        {
            return;
        }

        double span = Math.Max(_world.Width, Math.Max(_world.Height, _world.Depth));
        double minDistance = span * 0.7;
        double maxDistance = span * 4.0;

        if (_cameraDistance <= 0)
        {
            _cameraDistance = span * 1.9;
        }

        _cameraDistance = Math.Clamp(_cameraDistance, minDistance, maxDistance);
        _cameraPitch = Math.Clamp(_cameraPitch, -1.4, 1.4);

        double cosPitch = Math.Cos(_cameraPitch);
        double sinPitch = Math.Sin(_cameraPitch);
        double cosYaw = Math.Cos(_cameraYaw);
        double sinYaw = Math.Sin(_cameraYaw);

        double x = cosPitch * sinYaw;
        double y = sinPitch;
        double z = cosPitch * cosYaw;

        Vector3D direction = new(x, y, z);
        direction.Normalize();

        Vector3D position = direction * _cameraDistance;
        MainCamera.Position = new Point3D(position.X, position.Y, position.Z);
        MainCamera.LookDirection = new Vector3D(-position.X, -position.Y, -position.Z);
        MainCamera.UpDirection = new Vector3D(0, 1, 0);
        MainCamera.NearPlaneDistance = Math.Max(1, _cameraDistance * 0.05);
        MainCamera.FarPlaneDistance = _cameraDistance * 5;
    }

    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _isDragging = true;
        _lastMouse = e.GetPosition(ViewportHost);
        ViewportHost.CaptureMouse();
        ViewportHost.Focus();
    }

    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _isDragging = false;
        ViewportHost.ReleaseMouseCapture();
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        Point current = e.GetPosition(ViewportHost);
        System.Windows.Vector delta = current - _lastMouse;
        _lastMouse = current;

        _cameraYaw += delta.X * 0.005;
        _cameraPitch -= delta.Y * 0.005;
        UpdateCamera();
    }

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_world is null)
        {
            return;
        }

        double factor = 1 - (e.Delta * 0.0015);
        _cameraDistance *= factor;
        UpdateCamera();
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

        StatsText.Text = $"Particles: {_world.Particles.Count}\nSpeed: {_speed:0.00}x\nRestitution: {_world.Settings.Restitution:0.00}\nDepth: {_world.Depth:0}";
    }

    private float RandomRange(float min, float max)
    {
        return (float)(_random.NextDouble() * (max - min) + min);
    }

    private static MeshGeometry3D CreateSphereMesh(double radius, int thetaDiv, int phiDiv)
    {
        MeshGeometry3D mesh = new();
        for (int t = 0; t <= thetaDiv; t++)
        {
            double theta = Math.PI * t / thetaDiv;
            double sinTheta = Math.Sin(theta);
            double cosTheta = Math.Cos(theta);

            for (int p = 0; p <= phiDiv; p++)
            {
                double phi = 2 * Math.PI * p / phiDiv;
                double sinPhi = Math.Sin(phi);
                double cosPhi = Math.Cos(phi);

                double x = radius * sinTheta * cosPhi;
                double y = radius * cosTheta;
                double z = radius * sinTheta * sinPhi;

                mesh.Positions.Add(new Point3D(x, y, z));
                mesh.Normals.Add(new Vector3D(x, y, z));
                mesh.TextureCoordinates.Add(new Point((double)p / phiDiv, (double)t / thetaDiv));
            }
        }

        int stride = phiDiv + 1;
        for (int t = 0; t < thetaDiv; t++)
        {
            for (int p = 0; p < phiDiv; p++)
            {
                int a = t * stride + p;
                int b = a + stride;
                int c = b + 1;
                int d = a + 1;

                mesh.TriangleIndices.Add(a);
                mesh.TriangleIndices.Add(b);
                mesh.TriangleIndices.Add(c);

                mesh.TriangleIndices.Add(a);
                mesh.TriangleIndices.Add(c);
                mesh.TriangleIndices.Add(d);
            }
        }

        return mesh;
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

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void RebuildGuides()
    {
        if (_scene is null || _world is null)
        {
            return;
        }

        if (_guideGroup is not null)
        {
            _scene.Children.Remove(_guideGroup);
        }

        _guideGroup = BuildGuides();
        _scene.Children.Insert(3, _guideGroup);
    }

    private Model3DGroup BuildGuides()
    {
        Model3DGroup group = new();
        if (_world is null)
        {
            return group;
        }

        double w = _world.Width;
        double h = _world.Height;
        double d = _world.Depth;
        double thickness = Math.Max(1.2, Math.Min(w, h) / 200.0);
        double axisLength = Math.Min(w, Math.Min(h, d)) * 0.7;

        DiffuseMaterial xMaterial = new(new SolidColorBrush(Color.FromRgb(232, 90, 86)));
        DiffuseMaterial yMaterial = new(new SolidColorBrush(Color.FromRgb(92, 201, 140)));
        DiffuseMaterial zMaterial = new(new SolidColorBrush(Color.FromRgb(90, 168, 232)));
        DiffuseMaterial boxMaterial = new(new SolidColorBrush(Color.FromRgb(120, 128, 150)));

        group.Children.Add(CreateBoxMesh(axisLength, thickness * 1.2, thickness * 1.2, new Point3D(0, 0, 0), xMaterial));
        group.Children.Add(CreateBoxMesh(thickness * 1.2, axisLength, thickness * 1.2, new Point3D(0, 0, 0), yMaterial));
        group.Children.Add(CreateBoxMesh(thickness * 1.2, thickness * 1.2, axisLength, new Point3D(0, 0, 0), zMaterial));

        AddBoxWireframe(group, w, h, d, thickness, boxMaterial);
        return group;
    }

    private static void AddBoxWireframe(Model3DGroup group, double width, double height, double depth, double thickness, Material material)
    {
        double hx = width / 2.0;
        double hy = height / 2.0;
        double hz = depth / 2.0;

        group.Children.Add(CreateBoxMesh(width, thickness, thickness, new Point3D(0, hy, hz), material));
        group.Children.Add(CreateBoxMesh(width, thickness, thickness, new Point3D(0, hy, -hz), material));
        group.Children.Add(CreateBoxMesh(width, thickness, thickness, new Point3D(0, -hy, hz), material));
        group.Children.Add(CreateBoxMesh(width, thickness, thickness, new Point3D(0, -hy, -hz), material));

        group.Children.Add(CreateBoxMesh(thickness, height, thickness, new Point3D(hx, 0, hz), material));
        group.Children.Add(CreateBoxMesh(thickness, height, thickness, new Point3D(hx, 0, -hz), material));
        group.Children.Add(CreateBoxMesh(thickness, height, thickness, new Point3D(-hx, 0, hz), material));
        group.Children.Add(CreateBoxMesh(thickness, height, thickness, new Point3D(-hx, 0, -hz), material));

        group.Children.Add(CreateBoxMesh(thickness, thickness, depth, new Point3D(hx, hy, 0), material));
        group.Children.Add(CreateBoxMesh(thickness, thickness, depth, new Point3D(hx, -hy, 0), material));
        group.Children.Add(CreateBoxMesh(thickness, thickness, depth, new Point3D(-hx, hy, 0), material));
        group.Children.Add(CreateBoxMesh(thickness, thickness, depth, new Point3D(-hx, -hy, 0), material));
    }

    private static GeometryModel3D CreateBoxMesh(double sizeX, double sizeY, double sizeZ, Point3D center, Material material)
    {
        double hx = sizeX / 2.0;
        double hy = sizeY / 2.0;
        double hz = sizeZ / 2.0;

        Point3D p0 = new(center.X - hx, center.Y - hy, center.Z - hz);
        Point3D p1 = new(center.X + hx, center.Y - hy, center.Z - hz);
        Point3D p2 = new(center.X + hx, center.Y + hy, center.Z - hz);
        Point3D p3 = new(center.X - hx, center.Y + hy, center.Z - hz);
        Point3D p4 = new(center.X - hx, center.Y - hy, center.Z + hz);
        Point3D p5 = new(center.X + hx, center.Y - hy, center.Z + hz);
        Point3D p6 = new(center.X + hx, center.Y + hy, center.Z + hz);
        Point3D p7 = new(center.X - hx, center.Y + hy, center.Z + hz);

        MeshGeometry3D mesh = new();
        mesh.Positions = new Point3DCollection { p0, p1, p2, p3, p4, p5, p6, p7 };

        mesh.TriangleIndices = new Int32Collection
        {
            0,1,2, 0,2,3,
            4,6,5, 4,7,6,
            0,4,5, 0,5,1,
            1,5,6, 1,6,2,
            2,6,7, 2,7,3,
            3,7,4, 3,4,0
        };

        return new GeometryModel3D
        {
            Geometry = mesh,
            Material = material,
            BackMaterial = material
        };
    }
}



