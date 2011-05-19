using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RestFract.Generators.Distributed;
using RestFract.Generators;
using System.Net;
using Cloo;
using RestFract.Generators.OpenCL;

namespace SimpleCalcServer
{
  class Program
  {
    static void Main(string[] args)
    {
      if (args.Length <= 1)
      {
        System.Console.WriteLine(String.Format("Usage: {0} {{generator}} {{port}} [options]", System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location)));
        System.Console.WriteLine("  generator: the generator to use");
        System.Console.WriteLine("     s : SimpleCalculator");
        System.Console.WriteLine("     c : PreCompiledCalculator");
        System.Console.WriteLine("     t : ThreadCalculator with PreCompiledCalculator");
        System.Console.WriteLine("     g : OpenCLCalculator");
        System.Console.WriteLine("     gl: List avialable platforms and devices for OpenCL");
        System.Console.WriteLine("  port: the port to listen on");
        System.Console.WriteLine("  options: additional options for the generators");
        System.Console.WriteLine("     ThreadCalculator: [threadnum]");
        System.Console.WriteLine("       number of threads to use (default:2)");
        System.Console.WriteLine("     OpenCLCalculator: [platformnum] [devicenum]");
        System.Console.WriteLine("       The platform and device to use (default:0/0)");
        return;
      }
      CreateCalculatorFactory fact = null;
      switch (args[0])
      {
        case "s": fact = () => new SimpleCalculatorFactory(); break;
        case "c": fact = () => new PreCompiledCalculatorFactory(); break;
        case "t":
          {
            int tnum = 2;
            if (args.Length > 2 && (!int.TryParse(args[2], out tnum) || tnum <= 0))
            {
              System.Console.WriteLine("Invalid number of threads");
              return;
            }
            System.Console.WriteLine(String.Format("Using {0} threads", tnum));
            fact = () => new ThreadCalculatorFactory(tnum, new PreCompiledCalculatorFactory());
          }
          break;
        case "g":
          {
            int pnum = 0;
            int dnum = 0;
            if (args.Length > 3 && ((!int.TryParse(args[2], out pnum) || pnum < 0 || pnum > ComputePlatform.Platforms.Count)))
            {
              System.Console.WriteLine("Invalid platform number. Run with 'gl' generator to get the list of avialable platforms and devices.");
              return;
            }
            if (args.Length > 3 && ((!int.TryParse(args[3], out dnum) || dnum < 0 || dnum > ComputePlatform.Platforms[pnum].Devices.Count)))
            {
              System.Console.WriteLine("Invalid device number. Run with 'gl' generator to get the list of avialable platforms and devices.");
              return;
            }
            if (args.Length==3)
            {
              System.Console.WriteLine("Missing device number");
              return;
            }
            if (ComputePlatform.Platforms.Count == 0)
            {
              System.Console.WriteLine("No OpenCL Platform available");
              return;
            }
            System.Console.WriteLine(String.Format("Using {0} Device {1}", ComputePlatform.Platforms[pnum].Name, ComputePlatform.Platforms[pnum].Devices[dnum].Name));
            ComputePlatform selected = ComputePlatform.Platforms[pnum];
            var devices = new List<ComputeDevice>();
            devices.Add(selected.Devices[dnum]);
            ComputeContextPropertyList properties = new ComputeContextPropertyList(selected);
            ComputeContext context = new ComputeContext(devices, properties, null, IntPtr.Zero);
            fact = () => new OpenCLCalculatorFactory(context);
          }
          break;
        case "gl":
          for (int i = 0; i < ComputePlatform.Platforms.Count; i++)
          {
            System.Console.WriteLine("Platform " + i + ": " + ComputePlatform.Platforms[i].Name);
            var platform = ComputePlatform.Platforms[i];
            for (int ii = 0; ii < platform.Devices.Count; ii++)
            {
              System.Console.WriteLine("  Device " + ii + ": " + platform.Devices[ii].Name);
            }
          }
          return;
      }
      int port;
      if (!int.TryParse(args[1], out port) || port < 1024 || port > 65535)
      {
        System.Console.WriteLine("Invalid port specified. Must be in range {1024..65535}");
      }
      System.Console.WriteLine(String.Format("Starting server on port {0}",port));
      DistributedCalculatorServer s = new DistributedCalculatorServer(fact, IPAddress.Any, 6079);
      s.Run();
    }
  }
}
