public class DVDPlayer
{
    public void On() => Console.WriteLine("DVD Player is On");
    public void Play(string movie) => Console.WriteLine($"Playing movie: {movie}");
    public void Off() => Console.WriteLine("DVD Player is Off");
}

public class Projector
{
    public void On() => Console.WriteLine("Projector is On"); 
    public void Off() => Console.WriteLine("Projector is Off"); 
    public void SetInput(string input) => Console.WriteLine($"Projector input set to: {input}"); 
}

public class SoundSystem
{
    public void On() => Console.WriteLine("Sound System is On");
    public void Off() => Console.WriteLine("Sound System is Off");
    public void SetVolume(int level) => Console.WriteLine($"Sound System level set to: {level}");
}

public class Screen
{
    public void Up() => Console.WriteLine("Screen is Up");
    public void Down() => Console.WriteLine("Screen is Down");
}

public class HomeTheatreFacade
{
    private DVDPlayer? _dvdPlayer;
    private Projector? _projector;
    private SoundSystem? _soundSystem;
    private Screen? _screen;

    public HomeTheatreFacade(
        DVDPlayer dvdPlayer
        , Projector projector
        , SoundSystem soundSystem
        , Screen screen)
    {
        _dvdPlayer = dvdPlayer;
        _projector = projector;
        _soundSystem = soundSystem;
        _screen = screen;
    }

    public void WatchMovie(string movie)
    {
        Console.WriteLine("Get ready to watch movie");
        _screen!.Down();
        _projector!.On();
        _projector!.SetInput("DVD");
        _soundSystem!.On();
        _soundSystem!.SetVolume(10);
        _dvdPlayer!.On();
        _dvdPlayer!.Play(movie);
    }

    public void EndMovie()
    {
        Console.WriteLine("Shutting down Home Theatre");
        _dvdPlayer!.Off();
        _soundSystem!.Off();
        _projector!.Off();
        _screen!.Up();
    }
}

public class HomeTheatreFacadeDemo
{
    public static void Main()
    {
        DVDPlayer dvdPlayer = new DVDPlayer();
        Projector projector = new Projector();
        SoundSystem soundSystem = new SoundSystem();
        Screen screen = new Screen();

        HomeTheatreFacade homeTheatreFacade = new HomeTheatreFacade(dvdPlayer,  projector, soundSystem, screen);

        homeTheatreFacade.WatchMovie("Inception");
        homeTheatreFacade.EndMovie();
    }
}

/*
 * Facade Pattern
 * --------------
 * Facade Design Pattern provides a unified interface to a set of interfaces in a subsystem.
 * Facade is a structural design pattern that provides a simplified interface to a library, a framework, 
 * or any other complex set of classes.
 * 
 * LLD Usecase:
 * 
 */