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
                    OnPropertyChanged();
                    OnPlayerSratusChanged?.Invoke(this, value);
                }
            }
        }
        WriteableBitmap? _Image;
        public WriteableBitmap? Image { get => _Image; set { if (_Image != value) 
                { _Image = value; OnPropertyChanged(); OnImageSourceChanged?.Invoke(this, value); } } }

        public string Version { get => ffmpeg.av_version_info(); }

        public EventHandler<string>? OnError;
        public EventHandler<string>? OnSatus;
        public EventHandler<WriteableBitmap?>? OnImageSourceChanged;
        public EventHandler<PlayerSratus>? OnPlayerSratusChanged;


        static string ffpath = Path.Combine(Environment.CurrentDirectory, "FFMPEG");

        InternalRtspPlayer? _player;

        public FFMPGPlayer()
        {
            ffmpeg.RootPath = ffpath;
            ffmpeg.avdevice_register_all();
        }
        public void Play(string rtsp)
        {
            Stop();
            _player = new();
            _player.OnImageSourceChanged += PlayerImageChanged;
            _player.OnError += PLayerError;
            _player.OnSatus += PLayerStatus;
            _player.OnPlayerSratusChanged += PlayerSratusChanged;
            _player.Play(rtsp);
        }
        public void Stop()
        {
            if (_player != null)
            {
                _player.OnImageSourceChanged -= PlayerImageChanged;
                _player.OnError -= PLayerError;
                _player.OnSatus -= PLayerStatus;
                _player.OnPlayerSratusChanged -= PlayerSratusChanged;
                _player.Dispose();
                PlayerSratus = PlayerSratus.Stopped;
                _player = null;
            }
        }

        void PLayerError(object? o, string text) => OnError?.Invoke(this, text);
        void PLayerStatus(object? o, string text) => OnSatus?.Invoke(this, text);
        void PlayerImageChanged(object? o, WriteableBitmap? image) =>
            Application.Current.Dispatcher.Invoke(() => Image = image);        
        void PlayerSratusChanged(object? o, PlayerSratus stat)
        =>PlayerSratus=stat;

        public void Dispose()=>Stop();
        
    }



    public enum PlayerSratus
    {
        Idle, Connecting, Play, Stopped
    }

    internal unsafe class InternalRtspPlayer : IDisposable
    {
        PlayerSratus _PlayerSratus = PlayerSratus.Idle;
        internal PlayerSratus PlayerSratus
        {
            get => _PlayerSratus;
            private set
            {
                if (_PlayerSratus != value)
                {
                    _PlayerSratus = value;
                    OnPlayerSratusChanged?.Invoke(this, value);
                }
            }
        }
        internal EventHandler<string>? OnError;
        internal EventHandler<string>? OnSatus;
        internal EventHandler<WriteableBitmap>? OnImageSourceChanged;
        internal EventHandler<PlayerSratus>? OnPlayerSratusChanged;


        unsafe AVFormatContext* _pFormatContext;
        unsafe AVCodecContext* _pCodecContext;

        int _videoStreamIndex = -1;

        WriteableBitmap? _bitmap;

        object _lock = new object();

        internal void Play(string rtsp) => Task.Run(() => PlayRtspInternal(rtsp));

        void PlayRtspInternal(string rtsp)
        {
            try
            {
                lock (_lock)
                {
                    PlayerSratus = PlayerSratus.Connecting;
                }
                Status($"Opening rtsp {rtsp}");
                int err = 0; _videoStreamIndex = -1;
                var formatContext = ffmpeg.avformat_alloc_context();
                err = ffmpeg.avformat_open_input(&formatContext, rtsp, null, null);
                if (err < 0)
                {
                    ffmpeg.avformat_free_context(formatContext);
                    Error("Couldn't open open rtsp", err);
                    return;
                }
                _pFormatContext = formatContext;
                err = ffmpeg.avformat_find_stream_info(_pFormatContext, null);
                if (err < 0)
                {
                    Error("Failed to find information about the stream", err);
                    return;
                }

                FindVideoSreamIndex();
                if (_videoStreamIndex == -1)
                    Error("Video stream not found", 0);

                var codecpar = _pFormatContext->streams[_videoStreamIndex]->codecpar;
                var _pCodec = ffmpeg.avcodec_find_decoder(codecpar->codec_id);

                if (_pCodec == null)
                {
                    Error($"Codec {codecpar->codec_id} Not Found");
                    return;
                }

                _pCodecContext = ffmpeg.avcodec_alloc_context3(_pCodec);

                err = ffmpeg.avcodec_parameters_to_context(_pCodecContext, codecpar);
                if (err < 0)
                {
                    Error($"Codec Context not Inited", err);
                    return;
                }

                err = ffmpeg.avcodec_open2(_pCodecContext, _pCodec, null);
                if (err < 0)
                {
                    Error($"Codec Context not Inited", err);
                    return;
                }

                ProcessFrames();
            }
            catch (Exception e)
            {
                Error($"Exception {e.Message}");
            }
            finally
            {
                FreeResources();
            }

        }

        void ProcessFrames()
        {
            
            AVFrame* _pFrame = ffmpeg.av_frame_alloc(); ;
            AVPacket* _pPacket = ffmpeg.av_packet_alloc();
            SwsContext* _pSwsContext = null;
            try
            {
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


                byte_ptrArray4 dstData = new();
                int_array4 dstLinesize = new();

                byte[] buffer = new byte[width * height * 3];


                fixed (byte* pBuffer = buffer)
                {
                    ffmpeg.av_image_fill_arrays(
                        ref dstData, ref dstLinesize, pBuffer,
                        AVPixelFormat.AV_PIX_FMT_BGR24, width, height, 1
                    );
                    lock (_lock)
                    {
                        PlayerSratus = PlayerSratus.Play;
                    }
                    while (PlayerSratus == PlayerSratus.Play)
                    {
                        if (ffmpeg.av_read_frame(_pFormatContext, _pPacket) < 0)
                            break;

                        if (_pPacket->stream_index == _videoStreamIndex && PlayerSratus == PlayerSratus.Play)
                        {
                            if (ffmpeg.avcodec_send_packet(_pCodecContext, _pPacket) < 0 && PlayerSratus == PlayerSratus.Play) continue;
                            if (ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame) >= 0 && PlayerSratus == PlayerSratus.Play)
                            {
                                ffmpeg.sws_scale(
                                    _pSwsContext, _pFrame->data, _pFrame->linesize,
                                    0, height, dstData, dstLinesize
                                );
                                if (PlayerSratus == PlayerSratus.Play)
                                    Application.Current?.Dispatcher?.Invoke(() =>
                                    {
                                        if (_bitmap != null)
                                        {
                                            _bitmap.Lock();
                                            Marshal.Copy(buffer, 0, _bitmap.BackBuffer, buffer.Length);
                                            _bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                                            _bitmap.Unlock();
                                        }
                                    });
                            }
                        }
                        ffmpeg.av_packet_unref(_pPacket);
                    }
                }
            }
            catch
            {

            }
            finally
            {
                lock (_lock)
                {
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
            lock (_lock)
            {
                PlayerSratus = PlayerSratus.Stopped;
                if (_pCodecContext != null)
                {
                    var p = _pCodecContext;
                    ffmpeg.avcodec_free_context(&p);
                    _pCodecContext = null;
                }
                if (_pFormatContext != null)
                {
                    try
                    {
                        fixed (AVFormatContext** ppFormatContext = &_pFormatContext)
                        {
                            ffmpeg.avformat_close_input(ppFormatContext);
                            ffmpeg.avformat_free_context(_pFormatContext);
                        }
                    }
                    catch { }
                    _pFormatContext = null;
                }
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
        }
    }

}
