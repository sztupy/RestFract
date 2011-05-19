using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Numerics;
using System.IO;

namespace RestFract.Generators.Distributed
{
  public delegate ICalculatorFactory CreateCalculatorFactory();

  public class DistributedCalculatorServer
  {
    TcpListener _listen;
    CreateCalculatorFactory _factgen;

    public DistributedCalculatorServer(CreateCalculatorFactory factgen, IPAddress addr, int port)
    {
      _factgen = factgen;
      _listen = new TcpListener(addr,port);
    }

    public void Run()
    {
      BinaryFormatter bin = new BinaryFormatter();
      _listen.Start();
      while (true)
      {
        // TODO: Add interface to signal connections
        TcpClient client = _listen.AcceptTcpClient();
        var t = new Thread(() => MainThread(client));
        t.IsBackground = true;
        t.Start();
      }
    }

    public void MainThread(TcpClient client)
    {
      var s = client.GetStream();
      try
      {
        BinaryFormatter bin = new BinaryFormatter();
        BinaryReader br = new BinaryReader(s);
        ICalculatorFactory f = _factgen();
        ICalculator calc = null;
        List<ProcessLayer> LayerData = null;
        while (true)
        {
          int action = br.ReadInt32();
          if (action == -1) break;
          if (action == 1)
          {
            LayerData = (List<ProcessLayer>)bin.Deserialize(s);
            FractalType fractaltype = (FractalType)bin.Deserialize(s);
            string code = (string)bin.Deserialize(s);
            int deflayer = (int)bin.Deserialize(s);
            calc = f.GenFractalCalc(LayerData, fractaltype, code, LayerData[deflayer]);
          }
          if (action == 0)
          {
            LayerData = (List<ProcessLayer>)bin.Deserialize(s);
            double param = (double)bin.Deserialize(s);
            long count = (long)bin.Deserialize(s);
            calc.InitData(LayerData, param, count);
          }
          if (action == 3)
          {
            int count = br.ReadInt32();
            byte[] buf = new byte[count * 40];
            int pos = 0;
            while (pos != count * 40)
            {
              pos += s.Read(buf, pos, (int)(count * 40) - pos);
            }
            using (var ms = new MemoryStream(buf))
            using (var br2 = new BinaryReader(ms))
            {
              while (ms.Position != ms.Length)
              {
                int px, py;
                double r, i;
                Complex x, c;
                px = br2.ReadInt32();
                py = br2.ReadInt32();
                r = br2.ReadDouble();
                i = br2.ReadDouble();
                x = new Complex(r, i);
                r = br2.ReadDouble();
                i = br2.ReadDouble();
                c = new Complex(r, i);
                calc.AddPoint(px, py, x, c);
              }
            }
            calc.EndSend();
            buf = new byte[count * (2 * sizeof(int) + LayerData.Count * (4 * sizeof(int) + 12 * sizeof(double)))];
            using (var ms = new MemoryStream(buf))
            using (var br2 = new BinaryWriter(ms))
            {
              while (ms.Position != ms.Length)
              {
                int px, py;
                List<ProcessLayer> pl;
                calc.GetPoint(out px, out py, out pl);
                br2.Write(px);
                br2.Write(py);
                foreach (var p in pl)
                {
                  br2.Write(p.c_old2x.Real);
                  br2.Write(p.c_old2x.Imaginary);
                  br2.Write(p.c_oldx.Real);
                  br2.Write(p.c_oldx.Imaginary);
                  br2.Write(p.c_x.Real);
                  br2.Write(p.c_x.Imaginary);
                  br2.Write(p.c_resx.Real);
                  br2.Write(p.c_resx.Imaginary);
                  br2.Write(p.c_calc);
                  br2.Write(p.c_cmean);
                  br2.Write(p.c_cvarsx);
                  br2.Write(p.c_cvariance);
                  br2.Write(p.c_active ? 1 : 0);
                  br2.Write(p.c_isin ? 1 : 0);
                  br2.Write(p.c_n);
                  br2.Write(p.c_resn);
                }
              }
            }
            s.Write(buf, 0, buf.Length);
          }
          if (action == 4)
          {
            calc.EndGet(false);
          }
          if (action == 5)
          {
            calc.EndGet(true);
          }
        }
      }
      catch (SocketException)
      {
        // TODO: Add Interface to send exception information
      }
      catch (IOException)
      {
      }
      finally
      {
        s.Close();
      }
    }
  }
}
