// SPDX-License-Identifier: AGPL-3.0-only
// Spatial phone/chat presentation inspired by VI Translator core/src/phone.rs and chat.rs (AGPL-3.0).
using System.Buffers.Binary;
using System.IO.Compression;
namespace GameTranslator.Core;

public sealed record PanelCell(string Source,string Text,TextBox Box,string Background,string Foreground,
    double LineHeight,bool Bubble=false,bool Heading=false);
public sealed record PanelLayout(int Width,int Height,string Kind,string? Image,IReadOnlyList<PanelCell> Cells);

/// <summary>Keep OCR geometry and measured colours. Never flatten unrelated messages into one sentence.</summary>
public static class SpatialText
{
    public static PanelLayout Build(Frame frame,Reading reading,ReadingAreaKind kind)
    {
        var lines=reading.Lines.Where(l=>!string.IsNullOrWhiteSpace(l.Text)&&l.Box is not null)
            .OrderBy(l=>l.Box!.Value.Top).ThenBy(l=>l.Box!.Value.Left).Take(64).ToList();
        var groups=new List<List<TextLine>>();
        foreach(var line in lines)
        {
            var b=line.Box!.Value;var h=b.Bottom-b.Top;
            var group=groups.FindLast(g=>{
                var last=g[^1].Box!.Value;var overlap=Math.Min(last.Right,b.Right)-Math.Max(last.Left,b.Left);
                var gap=(b.Top-last.Bottom)*frame.Height;
                return gap>=-h*frame.Height*.2&&gap<=Math.Max(h,last.Bottom-last.Top)*frame.Height*.65
                    &&overlap>Math.Min(last.Right-last.Left,b.Right-b.Left)*.45
                    &&ColorDistance(Sample(frame,last),Sample(frame,b))<42
                    // Short menu entries remain separate; wrapped paragraphs and bubbles stay together.
                    &&(kind==ReadingAreaKind.Phone||g[^1].Text.Length>36);
            });
            if(group is null)groups.Add([line]);else group.Add(line);
        }
        var cells=new List<PanelCell>();var clean=new Frame((byte[])frame.Pixels.Clone(),frame.Width,frame.Height,frame.Stride);
        foreach(var g in groups.Take(24))
        {
            var b=g.Select(l=>l.Box!.Value).Aggregate((a,b)=>a.Union(b));
            var color=Sample(frame,b);var blue=color[2]-color[0]>45&&color[2]>110;
            var text=string.Join(' ',g.Select(l=>l.Text.Trim()));
            var medianH=g.Select(l=>l.Box!.Value.Bottom-l.Box.Value.Top).Order().ElementAt(g.Count/2);
            // Grow only into the surrounding margin, never into the neighbouring message.
            var pad=Math.Min(.012,medianH*.22);var box=new TextBox(Math.Max(0,b.Left-pad),Math.Max(0,b.Top-pad),Math.Min(1,b.Right+pad),Math.Min(1,b.Bottom+pad));
            var background=Hex(color);var foreground=color.Sum(x=>(int)x)>390&&!blue?"#20232A":"#FFFFFF";
            var title=kind==ReadingAreaKind.Phone&&b.Top<.12&&g.Count==1;
            cells.Add(new(text,"",box,background,foreground,medianH,kind==ReadingAreaKind.Phone&&!title,title));
            Fill(clean,box,color); // Preserve avatars, dividers, badges and the phone's original panel; remove English ink.
        }
        var image=kind==ReadingAreaKind.Phone?Png(clean):null;
        return new(frame.Width,frame.Height,kind==ReadingAreaKind.Phone?"phone":"spatial",image,cells);
    }
    // Phone screen search is confined to the selected ROI. Scenery outside it cannot become a phone.
    public static CropRegion? FindPhone(Frame frame)
    {
        if(frame.Width<90||frame.Height<150)return null;
        const int step=6;int w=frame.Width/step,h=frame.Height/step;var light=new bool[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            var c=Pixel(frame,x*step+step/2,y*step+step/2);var lo=c.Min();var hi=c.Max();light[y*w+x]=lo>125&&hi-lo<65;
        }
        var seen=new bool[light.Length];(int L,int T,int R,int B,int Count)? best=null;
        for(int start=0;start<light.Length;start++){
            if(!light[start]||seen[start])continue;var stack=new Stack<int>();stack.Push(start);seen[start]=true;
            int l=w,t=h,r=0,b=0,n=0;
            while(stack.TryPop(out var i)){int x=i%w,y=i/w;l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);b=Math.Max(b,y);n++;
                void Add(int at){if(!seen[at]&&light[at]){seen[at]=true;stack.Push(at);}}
                if(x>0)Add(i-1);if(x+1<w)Add(i+1);if(y>0)Add(i-w);if(y+1<h)Add(i+w);
            }
            var pw=(r-l+1)*step;var ph=(b-t+1)*step;
            if(pw<frame.Width*.14||ph<frame.Height*.38||ph<pw*1.1||ph>pw*4.5||n<((r-l+1)*(b-t+1))*.38)continue;
            if(best is null||n>best.Value.Count)best=(l,t,r,b,n);
        }
        return best is {} a?new((double)a.T*step/frame.Height,(double)(a.B+1)*step/frame.Height,(double)a.L*step/frame.Width,(double)(a.R+1)*step/frame.Width):null;
    }
    private static byte[] Pixel(Frame f,int x,int y){var i=Math.Clamp(y,0,f.Height-1)*f.Stride+Math.Clamp(x,0,f.Width-1)*4;return [f.Pixels[i+2],f.Pixels[i+1],f.Pixels[i]];}
    private static byte[] Sample(Frame f,TextBox b)
    {
        // Modal quantized colour ignores the thin glyphs; stable on both dark menus and white/blue bubbles.
        var colors=new Dictionary<int,int>();
        for(double y=b.Top;y<b.Bottom;y+=Math.Max(.003,(b.Bottom-b.Top)/12))for(double x=b.Left;x<b.Right;x+=Math.Max(.003,(b.Right-b.Left)/16)){
            var c=Pixel(f,(int)(x*f.Width),(int)(y*f.Height));var key=((c[0]/16)<<8)|((c[1]/16)<<4)|(c[2]/16);colors[key]=colors.GetValueOrDefault(key)+1;
        }
        var mode=colors.Count==0?0:colors.MaxBy(k=>k.Value).Key;return [(byte)(((mode>>8)&15)*16+8),(byte)(((mode>>4)&15)*16+8),(byte)((mode&15)*16+8)];
    }
    private static int ColorDistance(byte[] a,byte[] b)=>a.Zip(b,(x,y)=>Math.Abs(x-y)).Max();
    private static string Hex(byte[] c)=>$"#{c[0]:X2}{c[1]:X2}{c[2]:X2}";
    private static void Fill(Frame f,TextBox b,byte[] c){for(int y=(int)(b.Top*f.Height);y<Math.Min(f.Height,(int)Math.Ceiling(b.Bottom*f.Height));y++)for(int x=(int)(b.Left*f.Width);x<Math.Min(f.Width,(int)Math.Ceiling(b.Right*f.Width));x++){int i=y*f.Stride+x*4;f.Pixels[i]=c[2];f.Pixels[i+1]=c[1];f.Pixels[i+2]=c[0];f.Pixels[i+3]=255;}}
    /// <summary>Bounded lossless local preview; no screenshot is uploaded to a translation provider.</summary>
    private static string Png(Frame frame)
    {
        var ratio=Math.Min(1,Math.Min(360d/frame.Width,760d/frame.Height));int w=Math.Max(1,(int)(frame.Width*ratio)),h=Math.Max(1,(int)(frame.Height*ratio));
        using var output=new MemoryStream();output.Write(new byte[]{137,80,78,71,13,10,26,10});
        void Chunk(string name,byte[] data){var type=System.Text.Encoding.ASCII.GetBytes(name);Span<byte> n=stackalloc byte[4];BinaryPrimitives.WriteInt32BigEndian(n,data.Length);output.Write(n);output.Write(type);output.Write(data);uint crc=0xffffffff;foreach(var v in type.Concat(data)){crc^=v;for(int k=0;k<8;k++)crc=(crc&1)!=0?0xedb88320^(crc>>1):crc>>1;}BinaryPrimitives.WriteUInt32BigEndian(n,crc^0xffffffff);output.Write(n);}
        var header=new byte[13];BinaryPrimitives.WriteInt32BigEndian(header,w);BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4),h);header[8]=8;header[9]=2;Chunk("IHDR",header);
        using var packed=new MemoryStream();using(var zip=new ZLibStream(packed,CompressionLevel.Fastest,true))for(int y=0;y<h;y++){zip.WriteByte(0);for(int x=0;x<w;x++){var c=Pixel(frame,(int)(x/ratio),(int)(y/ratio));zip.Write(c);}}
        Chunk("IDAT",packed.ToArray());Chunk("IEND",[]);return Convert.ToBase64String(output.ToArray());
    }
}
