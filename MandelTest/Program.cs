using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RestFract;
using System.Numerics;
using RestFract.Color;
using RestFract.Output;
using RestFract.Generators;
using RestFract.Generators.OpenCL;
using Cloo;
using RestFract.Callbacks;
using System.Threading;
using RestFract.Generators.Distributed;

namespace MandelTest
{
  class Program
  {
    static void Main(string[] args)
    {
      if (args.Length < 2)
      {
        System.Console.WriteLine(String.Format("Usage: {0} {{type}} {{generator}} [options]", System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location)));
        System.Console.WriteLine("  type: the fractal to draw");
        System.Console.WriteLine("    0: simple mandelbrot, 32768 iterations at 400x400");
        System.Console.WriteLine("    1: x*x+c-1/n, 4096 iterations at 800x800");
        System.Console.WriteLine("    2: colored mandelbrot zoomed, 65536 iterations at 400x400");
        System.Console.WriteLine("  generator: the generator to use");
        System.Console.WriteLine("     s : SimpleCalculator");
        System.Console.WriteLine("     c : PreCompiledCalculator");
        System.Console.WriteLine("     t : ThreadCalculator with PreCompiledCalculator");
        System.Console.WriteLine("     g : OpenCLCalculator");
        System.Console.WriteLine("     gl: List avialable platforms and devices for OpenCL");
        System.Console.WriteLine("     d : DistributedCalculator");
        System.Console.WriteLine("  options: additional options for the generators");
        System.Console.WriteLine("     ThreadCalculator: [threadnum]");
        System.Console.WriteLine("       number of threads to use (default:2)");
        System.Console.WriteLine("     OpenCLCalculator: [platformnum] [devicenum]");
        System.Console.WriteLine("       The platform and device to use (default:0/0)");
        System.Console.WriteLine("     DistributedCalculator: {{ip1}} {{port1}} [ip2] [port2] ...");
        System.Console.WriteLine("       The hosts to connect to");
        return;
      }
      ICalculatorFactory fact = null;
      int type = 0;
      if (!int.TryParse(args[0], out type) || type<0 || type>2)
      {
        System.Console.WriteLine("Invalid fractal type");
        return;
      }
      switch (args[1])
      {
        case "s": fact = new SimpleCalculatorFactory(); break;
        case "c": fact = new PreCompiledCalculatorFactory(); break;
        case "t":
          {
            int tnum = 2;
            if (args.Length > 2 && (!int.TryParse(args[2], out tnum) || tnum <= 0))
            {
              System.Console.WriteLine("Invalid number of threads");
              return;
            }
            System.Console.WriteLine(String.Format("Using {0} threads", tnum));
            fact = new ThreadCalculatorFactory(tnum, new PreCompiledCalculatorFactory());
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
            if (args.Length == 3)
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
            fact = new OpenCLCalculatorFactory(context);
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
        case "d":
          {
            if (args.Length < 4)
            {
              System.Console.WriteLine("You need to specify a port and ip to connect to");
              return;
            }
            if (args.Length % 2 != 0)
            {
              System.Console.WriteLine("Invalid number of options");
              return;
            }
            fact = new DistributedCalculatorFactory();
            for (int i = 2; i < args.Length; i += 2)
            {
              System.Console.WriteLine("Connecting to {0}:{1}", args[i], args[i + 1]);
              int port;
              if (int.TryParse(args[i + 1], out port))
              {
                ((DistributedCalculatorFactory)fact).AddClient(args[i], port);
              }
              else
              {
                System.Console.WriteLine("Invalid Port");
                return;
              }
            }
          }
          break;
      }

      ColoredMandel mandel = new ColoredMandel(fact);
      FileOutput f = null;
      if (type == 1)
      {
        f = new FileOutput(800, 800);
      }
      else
      {
        f = new FileOutput(400, 400);
      }
      f.setBuffer(true);
      f.setFilename("save.bmp");

      DictGradientMap grad2 = new DictGradientMap("German_flag_smooth.ggr", true);
      DictGradientMap grad = new DictGradientMap(new ColorValue(true, 0, 0, 0, 1), new ColorValue(true, 1, 1, 1, 1));
      grad.Reverse();
      DictGradientMap grad3 = new DictGradientMap(new ColorValue(true, 0, 0, 0, 0), new ColorValue(true, 0, 1, 0, 1));

      mandel.Callback = new DotWriterCallback();

      switch (type)
      {
        case 0:
          mandel.AddLayer(new ProcessLayer(40, ConvCheck.MPL_CONVCHK_NORMAL, 32768, SeqType.MPL_SEQ_NORMAL, SeqCheck.MPL_CHECK_NORMAL, SeqType.MPL_SEQ_NORMAL),
                          new ColorLayer(LayerType.LAYER_TYPE_OUTSIDE, DataUsed.LAYER_DATAUSED_ITER, Interp.LAYER_INTERP_LOG, null, grad,0,0,4096));
          mandel.setDefaultLayer(0);
          break;
        case 1:
            mandel.Fractal = FractalType.FRACTAL_TYPE_DIVERGENT;
            if (args[1] == "gg")
            {
              mandel.setFunction("MULC(x,x) + c - (float2)(1.0f/n,0.0f)");
            }
            else
            {
              mandel.setFunction("x*x+c-1.0/n");
            }
            mandel.AddLayer(new ProcessLayer(40, ConvCheck.MPL_CONVCHK_NORMAL, 4096, SeqType.MPL_SEQ_NORMAL, SeqCheck.MPL_CHECK_NORMAL, SeqType.MPL_SEQ_NORMAL),
                            new ColorLayer(LayerType.LAYER_TYPE_OUTSIDE, DataUsed.LAYER_DATAUSED_ITER, Interp.LAYER_INTERP_LINEAR, null, grad));
            mandel.AddLayer(new ProcessLayer(64.0, ConvCheck.MPL_CONVCHK_NORMAL, 4096, SeqType.MPL_SEQ_STDDEV, SeqCheck.MPL_CHECK_TRIANGLE, SeqType.MPL_SEQ_STDDEV),
                            new ColorLayer(LayerType.LAYER_TYPE_BOTH, DataUsed.LAYER_DATAUSED_VALUE, Interp.LAYER_INTERP_LINEAR, null, grad2, 0, Math.PI));
            mandel.AddLayer(new ProcessLayer(4, ConvCheck.MPL_CONVCHK_NORMAL, 4096, SeqType.MPL_SEQ_MEAN, SeqCheck.MPL_CHECK_ORBIT_TRAP, SeqType.MPL_SEQ_MIN, Complex.One, Complex.Zero, OrbitTrap.MPL_ORBIT_TRAP_LINE),
                            new ColorLayer(LayerType.LAYER_TYPE_INSIDE, DataUsed.LAYER_DATAUSED_VALUE, Interp.LAYER_INTERP_LOG, null, grad2, 0.1, 0.0001));
            mandel.AddLayer(new ProcessLayer(4, ConvCheck.MPL_CONVCHK_NORMAL, 4096, SeqType.MPL_SEQ_MEAN, SeqCheck.MPL_CHECK_ORBIT_TRAP, SeqType.MPL_SEQ_MIN, Complex.Zero, Complex.Zero, OrbitTrap.MPL_ORBIT_TRAP_LINE),
                            new ColorLayer(LayerType.LAYER_TYPE_INSIDE, DataUsed.LAYER_DATAUSED_VALUE, Interp.LAYER_INTERP_LOG, null, grad3, 0.1, 0.0001));
            mandel.setDefaultLayer(0);
          break;
        case 2:
          mandel.setBounds(new Complex(0,0), 0.000066495);
          mandel.AddLayer(new ProcessLayer(40, ConvCheck.MPL_CONVCHK_NORMAL, 65536, SeqType.MPL_SEQ_NORMAL, SeqCheck.MPL_CHECK_NORMAL, SeqType.MPL_SEQ_NORMAL),
                          new ColorLayer(LayerType.LAYER_TYPE_OUTSIDE, DataUsed.LAYER_DATAUSED_ITER, Interp.LAYER_INTERP_LINEAR, null, grad,0,0,128));
          mandel.AddLayer(new ProcessLayer(40, ConvCheck.MPL_CONVCHK_NORMAL, 65536, SeqType.MPL_SEQ_NORMAL, SeqCheck.MPL_CHECK_ORBIT_TRAP, SeqType.MPL_SEQ_MIN, Complex.One, Complex.Zero, OrbitTrap.MPL_ORBIT_TRAP_LINE),
                          new ColorLayer(LayerType.LAYER_TYPE_INSIDE, DataUsed.LAYER_DATAUSED_VALUE, Interp.LAYER_INTERP_LOG, null, grad2, 0.1, 0.0000000001));
          mandel.AddLayer(new ProcessLayer(40, ConvCheck.MPL_CONVCHK_NORMAL, 65536, SeqType.MPL_SEQ_NORMAL, SeqCheck.MPL_CHECK_ORBIT_TRAP, SeqType.MPL_SEQ_MIN, Complex.Zero, Complex.Zero, OrbitTrap.MPL_ORBIT_TRAP_LINE),
                          new ColorLayer(LayerType.LAYER_TYPE_INSIDE, DataUsed.LAYER_DATAUSED_VALUE, Interp.LAYER_INTERP_LOG, null, grad3, 0.1, 0.0000000001));
          mandel.AddLayer(new ProcessLayer(40, ConvCheck.MPL_CONVCHK_NORMAL, 65536, SeqType.MPL_SEQ_NORMAL, SeqCheck.MPL_CHECK_ORBIT_TRAP, SeqType.MPL_SEQ_MIN, Complex.Zero, Complex.Zero, OrbitTrap.MPL_ORBIT_TRAP_GAUSS),
                          new ColorLayer(LayerType.LAYER_TYPE_INSIDE, DataUsed.LAYER_DATAUSED_VALUE, Interp.LAYER_INTERP_LOG, null, grad, 0.1, 0.0000000001));
          mandel.setDefaultLayer(0);
          break;
      }
      
      mandel.Draw(f, f.getWidth(), f.getHeight());
      System.Console.WriteLine();
      System.Console.WriteLine((f.getWidth()*f.getHeight())/mandel.getLastTimeUsed());
    }
  }
}
