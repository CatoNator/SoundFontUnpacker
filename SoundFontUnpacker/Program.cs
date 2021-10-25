using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SoundFontUnpacker
{
    class Program
    {
        static List<SF2Sound> Sounds;

        class SF2Sound
        {
            public string SoundName;

            public uint SampleStart = 0;
            public uint SampleSize = 0;

            public uint SampleLoopStart = 0;
            public uint SampleLoopLength = 0;

            public uint SampleRate = 0;

            public ushort SampleLink = 0;
            public ushort SampleType = 0;

            //public byte Pitch = 0; //???
            //public byte PitchCorrection = 0; //???

            public SF2Sound(string Name)
            {
                this.SoundName = Name;
            }
        }

        static void Main(string[] args)
        {
#if DEBUG
            Console.WriteLine("SoundFontUnpacker - extract *.sf2 files into *.wav files");
            string input = Console.ReadLine();
            Console.WriteLine("Input the output folder.");
            string output = Console.ReadLine();

            UnpackSF2(input, output);
#else
            Console.WriteLine("SoundFontUnpacker - extract *.sf2 files into *.wav files in a directory\nUsage: SoundFontUnpacker.exe sourcefile destfolder\nWritten by Catonator in Oct 2021");

            if (args.Length >= 2)
            {
                string input = args[0];
                string output = args[1];

                UnpackSF2(input, output);
            }
#endif
        }

        static void UnpackSF2(string SourceFile, string DestFolder)
        {
            Sounds = new List<SF2Sound>();

            byte[] SampleData;

            FileStream stream = new FileStream(SourceFile, FileMode.Open);
            BinaryReader reader = new BinaryReader(stream);

            //code here
            if (reader.ReadUInt32() != 0x46464952) //'RIFF'
                Console.WriteLine("Header mismatch!");

            //size of the SFBK block
            uint SFBKSize = reader.ReadUInt32();

            reader.ReadUInt32(); //'sfbk'

            /*if (reader.ReadUInt32() != 0x5453494C) //'sfbk'
                Console.WriteLine("Expected sfbk block");*/

            if (reader.ReadUInt32() != 0x5453494C) //'LIST'
                Console.WriteLine("Expected LIST block");
            
            //offset of LIST block from this point on
            uint LISTOffset = reader.ReadUInt32();
            LISTOffset += (uint)reader.BaseStream.Position; //LIST block is counter from the position at the end of the offset value

            Console.WriteLine("LISTOffset: " + LISTOffset);

            //seek to sample data start
            reader.BaseStream.Seek((long)LISTOffset, SeekOrigin.Begin);

            if (reader.ReadUInt32() != 0x5453494C) //'LIST'
                Console.WriteLine("Expected LIST block");

            //sample List size, for future reference
            uint LISTSize = reader.ReadUInt32();
            LISTOffset = (uint)reader.BaseStream.Position + LISTSize;

            if (reader.ReadUInt32() != 0x61746473) //'sdta'
                Console.WriteLine("Expected sdta block");

            if (reader.ReadUInt32() != 0x6C706D73) //'smpl'
                Console.WriteLine("Expected smpl data");

            //read sample data size
            uint SampleDataSize = reader.ReadUInt32();
            //read sample data
            SampleData = reader.ReadBytes((int)SampleDataSize);

            if (reader.ReadUInt32() != 0x5453494C) //'LIST'
                Console.WriteLine("Expected LIST block");

            //read size
            LISTSize = reader.ReadUInt32();
            LISTOffset = (uint)reader.BaseStream.Position + LISTSize;

            //pdta block
            if (reader.ReadUInt32() != 0x61746473) //'pdta'
                Console.WriteLine("Expected pdta block");

            //phdr block instrument info
            SkipSubchunk(reader);

            //pbag
            SkipSubchunk(reader);

            //pmod
            SkipSubchunk(reader);

            //pgen
            SkipSubchunk(reader);

            //inst
            SkipSubchunk(reader);

            //ibag
            SkipSubchunk(reader);

            //imod
            SkipSubchunk(reader);

            //igen
            SkipSubchunk(reader);

            //shdr
            //Sample data!!! finally

            //temp
            if (reader.ReadUInt32() != 0x72646873) //'shdr'
                Console.WriteLine("Expected shdr block");

            uint SHDRSize = reader.ReadUInt32();
            int SHDREntryCount = ((int)SHDRSize / 46) - 1; //always has at least 2 entries, one of them is padding

            for (int i = 0; i < SHDREntryCount; i++)
            {
                Sounds.Add(UnpackSample(reader));
            }

            reader.Close();

            for (int i = 0; i < Sounds.Count; i++)
            {
                WriteWave(Sounds[i], SampleData, DestFolder);
            }
            //WriteWave(Sounds[0], SampleData, DestFolder);
        }

        static void SkipList(BinaryReader reader)
        {
            if (reader.ReadUInt32() != 0x5453494C) //'LIST'
                Console.WriteLine("Expected LIST block");

            uint LISTOffset = reader.ReadUInt32();
            LISTOffset += (uint)reader.BaseStream.Position; //LIST block is counter from the position at the end of the offset value

            //seek to sample data start
            reader.BaseStream.Seek((long)LISTOffset, SeekOrigin.Begin);

            Console.WriteLine("Seeking past useless LIST block");
        }

        static void SkipSubchunk(BinaryReader reader)
        {
            string SubchunkName = "";

            for (int i = 0; i < 4; i++)
                SubchunkName += (char)reader.ReadByte();

            uint LISTOffset = reader.ReadUInt32();
            LISTOffset += (uint)reader.BaseStream.Position; //LIST block is counter from the position at the end of the offset value

            //seek to sample data start
            reader.BaseStream.Seek((long)LISTOffset, SeekOrigin.Begin);

            Console.WriteLine("Seeking past subchunk " + SubchunkName);
        }

        static SF2Sound UnpackSample(BinaryReader reader)
        {
            string SampleName = "";

            for (int c = 0; c < 20; c++)
            {
                byte NextChar = reader.ReadByte();

                if (NextChar != 0)
                    SampleName += (char)NextChar;
            }

            Console.WriteLine("sample name '" + SampleName + "'");

            SF2Sound NewSound = new SF2Sound(SampleName);

            uint SampleStart = reader.ReadUInt32();
            uint SampleEnd = reader.ReadUInt32();

            NewSound.SampleStart = SampleStart;
            NewSound.SampleSize = (SampleEnd - SampleStart);

            Console.WriteLine("sample start " + NewSound.SampleStart + " sample size " + NewSound.SampleSize);

            uint LoopStart = reader.ReadUInt32();
            uint LoopEnd = reader.ReadUInt32();

            //loop start is counted from the start of the sample field
            NewSound.SampleLoopStart = LoopStart - SampleStart;
            NewSound.SampleLoopLength = (LoopEnd - LoopStart);

            Console.WriteLine("loop start " + NewSound.SampleLoopStart + " loop length " + NewSound.SampleLoopLength);

            uint SampleRate = reader.ReadUInt32();

            NewSound.SampleRate = SampleRate;

            Console.WriteLine("sample rate " + SampleRate);

            reader.ReadByte(); //original pitch
            reader.ReadByte(); //pitch correction

            ushort SampleLink = reader.ReadUInt16(); //sample link
            ushort SampleType = reader.ReadUInt16(); //sample type - ???

            //need to be taken into account when writing sample
            NewSound.SampleLink = SampleLink;
            NewSound.SampleType = SampleType;

            Console.WriteLine("sample type " + SampleType + " sample link " + SampleLink);

            return NewSound;
        }

        static void WriteWave(SF2Sound Sound, byte[] SampleData, string DestFolder)
        {
            int side = 0; //0 = mono, 1 = left, 2 = right
            SF2Sound LinkedSound = Sound;
            
            switch (Sound.SampleType)
            {
                case 8:
                case 32776:
                    Console.WriteLine("Ignored linked sample");
                    return;
                case 1:
                    //Console.WriteLine("Mono sample");
                    side = 0;
                    break;
                case 2:
                    //Console.WriteLine("Left sample");
                    side = 1;
                    LinkedSound = Sounds[Sound.SampleLink];
                    break;
                case 3:
                    //Console.WriteLine("Right sample");
                    side = 2;
                    LinkedSound = Sounds[Sound.SampleLink];
                    break;
                case 32769:
                case 32770:
                case 32772:
                    Console.WriteLine("ROM samples unsupported");
                    return;
            }

            //ensuring that filename is legal in windows
            Regex IllegalRegex = new Regex("(^(PRN|AUX|NUL|CON|COM[1-9]|LPT[1-9]|(\\.+)$)(\\..*)?$)|(([\\x00-\\x1f\\\\?*:\";‌​|/<>])+)|([\\. ]+)", RegexOptions.IgnoreCase);

            string FileName = IllegalRegex.Replace(Sound.SoundName, "");

            FileStream outstream = File.Create(DestFolder + "\\" + FileName + ".wav");
            BinaryWriter writer = new BinaryWriter(outstream);

            //RIFF
            writer.Write((byte)'R');
            writer.Write((byte)'I');
            writer.Write((byte)'F');
            writer.Write((byte)'F');

            //get channel count, calculate the chunk size
            ushort Channels = 1;

            if (side != 0)
                Channels = 2;

            //chunksize
            //temp
            //WAVE + fmt chunk size + data chunk header + data chunk size
            uint WaveChunkSize = 4 + 24 + 8 + (Sound.SampleSize * Channels * 2);

            writer.Write((uint)WaveChunkSize);

            //'WAVE', chunk identifier
            writer.Write((byte)'W');
            writer.Write((byte)'A');
            writer.Write((byte)'V');
            writer.Write((byte)'E');

            //fmt subchunk
            writer.Write((byte)'f');
            writer.Write((byte)'m');
            writer.Write((byte)'t');
            writer.Write((byte)' ');

            //subchunk size
            writer.Write((uint)16); //temp?

            //format - PCM (always)
            writer.Write((ushort)1);

            //channels
            writer.Write((ushort)Channels);

            //sample rate
            writer.Write(Sound.SampleRate);

            //data rate - sample rate * bitdepth * channels
            uint DataRate = (Sound.SampleRate * 2 * Channels);
            writer.Write(DataRate);

            //block size - bitdepth * channels
            ushort BlockSize = (ushort)(2 * Channels);
            writer.Write(BlockSize);

            //bitdepth
            writer.Write((ushort)16);

            //smpl subchunk for loops
            writer.Write((byte)'s');
            writer.Write((byte)'m');
            writer.Write((byte)'p');
            writer.Write((byte)'l');

            //subchunk size
            writer.Write((uint)60); //temp?

            //manufacturer (N/A)
            writer.Write((uint)0);

            //product (N/A)
            writer.Write((uint)0);

            //sampling period in nanoseconds
            uint SamplingPeriod = (1 / Sound.SampleRate) * 1000000000;
            writer.Write(SamplingPeriod);

            //default note (60 is C-5 - tbd?)
            writer.Write((uint)60);

            //MIDI note fraction -  defaulted to zero
            writer.Write((uint)0);

            //SMPTE format
            writer.Write((uint)0);

            //SMPTE offset, this is defaulted to zero (who wants to delay their sample playback by 23 hours?)
            writer.Write((uint)0);

            //loop count - always 1
            writer.Write((uint)1);

            //sample data - nothin special here
            writer.Write((uint)0);

            //loop time
            //4-byte id
            writer.Write((byte)'l');
            writer.Write((byte)'o');
            writer.Write((byte)'o');
            writer.Write((byte)'p');

            //loop type, default is zero
            writer.Write((uint)0);

            //loop start
            writer.Write(Sound.SampleLoopStart);

            //loop end
            writer.Write(Sound.SampleLoopStart + Sound.SampleLoopLength);

            //fraction (N/A)
            writer.Write((uint)0);

            //loop count - 0 is infinite
            writer.Write((uint)0);

            //data chunk
            writer.Write((byte)'d');
            writer.Write((byte)'a');
            writer.Write((byte)'t');
            writer.Write((byte)'a');

            //data size
            uint DataSize = Sound.SampleSize * Channels * 2;
            writer.Write(DataSize);

            byte[] AudioData = new byte[DataSize];

            uint SampleStart = (Sound.SampleStart * 2);
            uint LinkStart = (LinkedSound.SampleStart * 2);

            for (int i = 0; i < Sound.SampleSize; i++)
            {
                int ind = i * 2;
                
                if (side == 0)
                {
                    AudioData[ind] = SampleData[SampleStart + ind];
                    AudioData[ind + 1] = SampleData[SampleStart + ind + 1];
                }
                //stereo samples
                else if (side == 1) //left sample, with right linked
                {
                    AudioData[ind] = SampleData[SampleStart + ind];
                    AudioData[ind + 1] = SampleData[SampleStart + ind + 1];

                    AudioData[ind + 2] = SampleData[LinkStart + ind];
                    AudioData[ind + 3] = SampleData[LinkStart + ind + 1];
                }
                else //right sample, with left linked
                {
                    AudioData[ind] = SampleData[LinkStart + ind];
                    AudioData[ind + 1] = SampleData[LinkStart + ind + 1];

                    AudioData[ind + 2] = SampleData[SampleStart + ind];
                    AudioData[ind + 3] = SampleData[SampleStart + ind + 1];
                }
            }

            writer.Write(AudioData);

            writer.Close();
        }
    }
}
