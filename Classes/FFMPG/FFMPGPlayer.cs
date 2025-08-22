using FFmpeg.AutoGen;
using IPCamPlayer.Helpers.VM;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IPCamPlayer.Classes.FFMPG
{
    public unsafe class FFMPGPlayer : VMNotifyPropretyChanged, IDisposable
    {
        PlayerSratus _PlayerSratus = PlayerSratus.Idle;
        public PlayerSratus PlayerSratus
        {
            get => _PlayerSratus;
            private set
            {
                if (_PlayerSratus != value)
                {
                    _PlayerSratus = value;
                    OnPlayerSratusChanged?.Invoke(this, value);
                    OnPropertyChanged();
                }
            }
        }
        public string Version { get => ffmpeg.av_version_info(); }
        public EventHandler<string>? OnError;
        public EventHandler<string>? OnSatus;
        public EventHandler<WriteableBitmap>? OnImageSourceChanged;
        public EventHandler<PlayerSratus>? OnPlayerSratusChanged;


        static string ffpath = Path.Combine(Environment.CurrentDirectory, "FFMPEG");

        unsafe AVFormatContext* _pFormatContext;
        AVCodecContext* _pCodecContext;

        int _videoStreamIndex = -1;

        WriteableBitmap? _bitmap;

        object _lock = new object();
        bool _isbusy = false;
        public FFMPGPlayer()
        {
            ffmpeg.RootPath = ffpath;
            ffmpeg.avdevice_register_all();
        }

        public void PlayRtspInternal(string rtps)
        {
            //lock (_lock)
            //{
            //    if (_isbusy) { Status(""); return; }
            //    _isbusy = true;
            //}
            try
            {
                PlayerSratus = PlayerSratus.Connecting;
                Status($"Opening rtsp {rtps}");
                int err = 0; _videoStreamIndex = -1;
                _pFormatContext = ffmpeg.avformat_alloc_context();
                var cont = _pFormatContext;
                err = ffmpeg.avformat_open_input(&cont, rtps, null, null);
                if (err < 0)
                    Error("Couldn't open open rtsp", err);
                err = ffmpeg.avformat_find_stream_info(_pFormatContext, null);
                if (err < 0)
                    Error("Failed to find information about the stream", err);

                FindVideoSreamIndex();
                if (_videoStreamIndex == -1)
                    Error("Video stream not found", 0);

                var codecpar = _pFormatContext->streams[_videoStreamIndex]->codecpar;
                var _pCodec = ffmpeg.avcodec_find_decoder(codecpar->codec_id);

                if (_pCodec == null)
                    Error($"Codec {codecpar->codec_id} Not Found");

                _pCodecContext = ffmpeg.avcodec_alloc_context3(_pCodec);

                err = ffmpeg.avcodec_parameters_to_context(_pCodecContext, codecpar);
                if (err <0)
                    Error($"Codec Context not Inited", err);

                err = ffmpeg.avcodec_open2(_pCodecContext, _pCodec, null);
                if (err < 0)
                    Error($"Codec Context not Inited", err);

                ProcessFrames();
            }
            catch (Exception e)
            {
                Error($"Exception {e.Message}");
            }
            finally
            {
                //_isbusy = false;
                FreeResources();
            }

        }

        void ProcessFrames()
        {
            AVFrame* _pFrame = ffmpeg.av_frame_alloc(); ;
            AVFrame* _poutputframe = ffmpeg.av_frame_alloc();
            AVPacket* _pPacket = ffmpeg.av_packet_alloc(); 
            SwsContext* _pSwsContext = null;
            try
            {
                Thread.Sleep(100);
                int width = _pCodecContext->width;
                int height = _pCodecContext->height;
                _pSwsContext = ffmpeg.sws_getContext(
                    width, height, _pCodecContext->pix_fmt,
                    width, height, AVPixelFormat.AV_PIX_FMT_BGR24, ffmpeg.SWS_FAST_BILINEAR, null, null, null
                );
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr24, null);
                    OnImageSourceChanged?.Invoke(this, _bitmap);
                });
               

                byte_ptrArray4 dstData = new byte_ptrArray4();
                int_array4 dstLinesize = new int_array4();

                _poutputframe->data[0] = dstData[0];
                _poutputframe->linesize[0] = dstLinesize[0];

                int bufferSize = ffmpeg.av_image_alloc(ref dstData, ref dstLinesize, width,
                    height, AVPixelFormat.AV_PIX_FMT_BGR24, 1);

                PlayerSratus = PlayerSratus.Play;
                while (PlayerSratus == PlayerSratus.Play)
                {
                    int ret = ffmpeg.av_read_frame(_pFormatContext, _pPacket);
                    if (ret < 0)
                    {
                        if (ret == ffmpeg.AVERROR_EOF)
                            break;
                        continue;
                    }

                    if (_pPacket->stream_index == _videoStreamIndex)
                    {
                        int err=ffmpeg.avcodec_send_packet(_pCodecContext, _pPacket);
                        err = ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame);
                        string s=err.av_errorToString();
                        if (err >= 0)
                        {
                            ffmpeg.sws_scale(
                                _pSwsContext, _pFrame->data, _pFrame->linesize,
                                0, height, dstData, dstLinesize
                            );
                            //try
                            //{
                            //    Marshal.ReadByte((IntPtr)_poutputframe->data[0]);
                            //}
                            //catch (AccessViolationException)
                            //{
                            //    continue; // Пам’ять недоступна
                            //}

                            _poutputframe->width = width;
                            _poutputframe->height = height;
                            _poutputframe->format = _pFrame->format;
                            int stride = _poutputframe->linesize[0];
                            
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _bitmap.Lock();
                                for (int y = 0; y < height; y++)
                                {
                                    IntPtr src = (IntPtr)(_poutputframe->data[0] + y * _poutputframe->linesize[0]);
                                    Int32Rect rect = new Int32Rect(0, y, width, 1);
                                    _bitmap.WritePixels(rect, src, stride, stride);
                                }
                                _bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                                _bitmap.Unlock();
                            });
                        }
                    }
                    ffmpeg.av_packet_unref(_pPacket);
                }
            }
            catch
            {

            }
            finally
            {
                if (_poutputframe != null)
                {
                    var p = _poutputframe;
                    ffmpeg.av_frame_free(&p);
                    _poutputframe = null;
                }
                if (_pFrame != null)
                {
                    ffmpeg.av_frame_free(&_pFrame);
                    _pFrame = null;
                }
                if (_pPacket != null)
                {
                    ffmpeg.av_packet_free(&_pPacket);
                    _pPacket = null;
                }
                if (_pSwsContext != null)
                {
                    ffmpeg.sws_freeContext(_pSwsContext);
                    _pSwsContext = null;
                }
                PlayerSratus = PlayerSratus.Stopped;
            }
        }

        int FindVideoSreamIndex()
        {
            _videoStreamIndex = -1;
            if (_pFormatContext != null && _pFormatContext->nb_streams > 0)
                for (int i = 0; i < _pFormatContext->nb_streams; i++)
                {
                    if (_pFormatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                    {
                        _videoStreamIndex = i;
                        break;
                    }
                }
            return _videoStreamIndex;
        }

        void FreeResources()
        {
            PlayerSratus = PlayerSratus.Stopped;
            if (_pCodecContext != null)
            {
                var p = _pCodecContext;
                ffmpeg.avcodec_free_context(&p);
                _pFormatContext = null;
            }
            if (_pFormatContext != null)
            {
                ffmpeg.avformat_free_context(_pFormatContext);
                _pFormatContext = null;
            }
        }

        void Error(string errtext, int errnum = 0)
        {
            OnError?.Invoke(this, $"FFmpeg Error: {errtext}.{(errnum != 0 ? errnum.av_errorToString() : "")}");
            FreeResources();
        }
        void Status(string text) => OnSatus?.Invoke(this, $"FFmpeg info: {text}");

        public void Dispose()
        {
            PlayerSratus = PlayerSratus.Stopped;
            FreeResources();
        }
    }

    public enum PlayerSratus
    {
        Idle, Connecting, Play, Stopped
    }

}
