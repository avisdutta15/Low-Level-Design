public class Video
{
    private string _videoName;
    public Video(string videoName) => _videoName = videoName;
}

public interface VideoDownloader
{
    public Video GetVideo(string videoName);
}

public class RealVideoDownloader : VideoDownloader
{ 
    public Video GetVideo(string videoName)
    {
        Console.WriteLine("Connecting to https://www.youtube.com/");
        Console.WriteLine("Downloading Video");
        Console.WriteLine("Retrieving Video Metadata");
        return new Video(videoName);
    }
}

public class ProxyVideoDownloader : VideoDownloader
{
    private Dictionary<string, Video> videoCache = new();
    private VideoDownloader downloader = new RealVideoDownloader();

    public Video GetVideo(string videoName)
    {
        if (!videoCache.ContainsKey(videoName))
        {
            var video = downloader.GetVideo(videoName);
            videoCache.Add(videoName, video);
        }
        Console.WriteLine("Retrieving video from cache...");
        Console.WriteLine("-----------------------");
        return videoCache[videoName];
    }
}

public class ProxyDemo
{
    public static void Main()
    {
        VideoDownloader videoDownloader = new ProxyVideoDownloader();
        videoDownloader.GetVideo("geekific");
        videoDownloader.GetVideo("geekific");
        videoDownloader.GetVideo("likeNsub");
        videoDownloader.GetVideo("likeNsub");
        videoDownloader.GetVideo("geekific");
    }
}

/*
    Types of Proxies:
    -----------------
    Virtual Proxy: Lazy initialization of expensive objects
    Protection Proxy: Access control
    Remote Proxy: Local representation of remote object
    Caching Proxy: Cache results of expensive operations

    How to use in LLD Interview?
    ----------------------------
    It can be used where we want to perform SOMETHING before or after the request is sent/recieved.
    Caching for database repo
    Logging additional logs
    Access control a service -> e.g. DeleteEmployee can only be called by member who are admin.
                                     we can have this logic inside the proxy.

    Usual Structure:
    ---------------
    Create an interface from the real subject with the same method signature.
    Implement the interface in the proxy class.
    Apply cross cutting logic to the methods in the proxy class.


    // The Subject interface declares common operations for both RealSubject and
    // the Proxy.
    public interface ISubject
    {
        void Request();
    }
    
    // The RealSubject contains some core business logic. Usually, RealSubjects
    // are capable of doing some useful work which may also be very slow or
    // sensitive
    class RealSubject : ISubject
    {
        public void Request()
        {
            Console.WriteLine("RealSubject: Handling Request.");
        }
    }

    // The Proxy has an interface identical to the RealSubject.
    class Proxy : ISubject
    {
        private RealSubject _realSubject;
        
        public Proxy(RealSubject realSubject)
        {
            this._realSubject = realSubject;
        }
        
        // The most common applications of the Proxy pattern are lazy loading,
        // caching, controlling the access, logging, etc. A Proxy can perform
        // one of these things and then, depending on the result, pass the
        // execution to the same method in a linked RealSubject object.
        public void Request()
        {
            if (this.CheckAccess())
            {
                this._realSubject.Request();

                this.LogAccess();
            }
        }
        
        public bool CheckAccess()
        {
            // Some real checks should go here.
            Console.WriteLine("Proxy: Checking access prior to firing a real request.");

            return true;
        }
        
        public void LogAccess()
        {
            Console.WriteLine("Proxy: Logging the time of request.");
        }
    }
    
    public class Client
    {
        // The client code is supposed to work with all objects (both subjects
        // and proxies) via the Subject interface in order to support both real
        // subjects and proxies. In real life, however, clients mostly work with
        // their real subjects directly. In this case, to implement the pattern
        // more easily, you can extend your proxy from the real subject's class.
        public void ClientCode(ISubject subject)
        {
            // ...
            
            subject.Request();
            
            // ...
        }
    }
 */