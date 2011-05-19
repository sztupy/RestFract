using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.IO;
using RestFract.Color;

namespace RestFract.Output
{
  public class FileOutput : FlattenedOutput, IMandelOutput
  {
    public FileOutput(int width, int height)
      : base()
    {
      buffered = false;
      c_width = width;
      c_height = height;
    }

    public virtual void setSize(int width, int height) { c_width = width; c_height = height; }
    public virtual int getWidth() { return c_width; }
    public virtual int getHeight() { return c_height; }

    public override void SetNextLine(int flags, int y)
    {
      if (!buffered)
      {
        if (y != getHeight()) file.Seek(pos + (getHeight() - 1 - y) * pitch, SeekOrigin.Begin);
      }
    }

    public override void SetInitDraw()
    {
       if (buffered) {
            pitch = getWidth()*3;
            if (pitch%4!=0) pitch+=4-pitch%4;
            filesize = 54+pitch*getHeight();
            buf = new byte[filesize+10];
            byte[] tmp;
            for (int i=0; i<57; i++) buf[i]=0;
            buf[0]=66;
            buf[1]=77;
            int p=2;
            tmp = BitConverter.GetBytes(filesize);
            foreach(byte b in tmp) {buf[p] = b; p++;}
            p+=4;
            tmp = BitConverter.GetBytes((int)54);
            foreach(byte b in tmp) {buf[p] = b; p++;}
            tmp = BitConverter.GetBytes((int)40);
            foreach(byte b in tmp) {buf[p] = b; p++;}
            tmp = BitConverter.GetBytes((int)getWidth());
            foreach(byte b in tmp) {buf[p] = b; p++;}
            tmp = BitConverter.GetBytes((int)getHeight());
            foreach(byte b in tmp) {buf[p] = b; p++;}
            buf[p] = 1; p++; p++;
            buf[p] = 24; p++; p++;
            pos=54;
            filesize = 54+pitch*getHeight();
          } else {
            file = new FileStream(c_filename,FileMode.Create);
            byte[] buffer = new byte[2];
            pitch = getWidth()*3;
            if (pitch%4!=0) pitch+=4-pitch%4;
            int filesize = 54+pitch*getHeight();
            buffer[0]=66;
            buffer[1]=77;
            file.Write(buffer, 0, 2);
            file.Write(BitConverter.GetBytes(filesize), 0, 4);
            file.Write(BitConverter.GetBytes((int)0), 0, 4);
            file.Write(BitConverter.GetBytes((int)54), 0, 4);
            file.Write(BitConverter.GetBytes((int)40), 0, 4);
            file.Write(BitConverter.GetBytes((int)getWidth()), 0, 4);
            file.Write(BitConverter.GetBytes((int)getHeight()), 0, 4);
            file.Write(BitConverter.GetBytes((short)1), 0, 2);
            file.Write(BitConverter.GetBytes((short)24), 0, 2);
            file.Write(BitConverter.GetBytes((int)0), 4, 0);
            file.Write(BitConverter.GetBytes((int)0), 4, 0);
            file.Write(BitConverter.GetBytes((int)0), 4, 0);
            file.Write(BitConverter.GetBytes((int)0), 4, 0);
            file.Write(BitConverter.GetBytes((int)0), 4, 0);
            file.Write(BitConverter.GetBytes((int)0), 4, 0);
            pos=54;
            byte[] bbb = new byte[pitch];
            for (int i = 0; i < getHeight(); i++) file.Write(bbb, 0, pitch);
            file.Seek(pos + (getHeight() - 1) * pitch, SeekOrigin.Begin);
          }
    }

    override public void SetEndDraw()
    {
      if (buffered)
      {
        using (file = new FileStream(c_filename, FileMode.Create))
        {
          file.Write(buf, 0, buf.Length);
        }
      }
      else
      {
        file.Close();
      }
    }

    public override void SetPoint(int flags, int x, int y, ColorValue c)
    {
      if (!buffered) {
        if (flags!=2) file.Seek(pos+(getHeight()-1-y)*pitch+x*3, SeekOrigin.Begin);
        byte[] buffer = new byte[3];
        buffer[0] = (byte)Math.Floor(c.Blue*255);
        buffer[1] = (byte)Math.Floor(c.Green * 255);
        buffer[2] = (byte)Math.Floor(c.Red * 255);
        file.Write(buffer, 0, 3);
      } else {
        buf[pos+(getHeight()-1-y)*pitch+x*3] = (byte)Math.Floor(c.Blue*255);
        buf[pos+(getHeight()-1-y)*pitch+x*3+1] = (byte)Math.Floor(c.Green*255);
        buf[pos+(getHeight()-1-y)*pitch+x*3+2] = (byte)Math.Floor(c.Red*255);
      }
    }

    public virtual void setBuffer(bool buf) { buffered = buf; }
    public virtual bool getBuffer() { return buffered; }
    public virtual void setFilename(string filename) { c_filename = filename; }
    public virtual string getFilename() { return c_filename; }

    private byte[] buf;
    private bool buffered;
    private int filesize;
    private int pos;
    private int pitch;
    private int c_width, c_height;
    private string c_filename;
    private FileStream file;
  }
}

