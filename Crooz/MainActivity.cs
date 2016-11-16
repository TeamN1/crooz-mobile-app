using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Provider;
using Android.Widget;
using Java.IO;
using SharedProject;
using Android.Views;
using Android.Hardware;
using System.Timers;
using System.Threading.Tasks;
using Android.Locations;
using System.Linq;
using Android.Util;
using Android.Runtime;
using RestSharp;
using Android.Telephony;
using Java.Util;
using Android.Media;

namespace Crooz
{
    [Activity(Label = "Crooz", MainLauncher = true, Icon = "@drawable/icon", ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : Activity, TextureView.ISurfaceTextureListener, Android.Hardware.Camera.IShutterCallback, Android.Hardware.Camera.IPictureCallback, ILocationListener
    {
        public static File _file;
        public static File _dir;
        public static Bitmap _bitmap;

        private TextView _resultTextView;
        private TextView _emotionDetailsTextView;
        private bool _isCaptureMode = true;
        Android.Hardware.Camera _camera;
        TextureView _textureView;
        System.Timers.Timer photoTimer;

        MediaPlayer _player;

        Location _currentLocation;
        LocationManager _locationManager;

        string _locationProvider;
        TextView _locationText;

        string _currentSession;
        string _deviceID;

        string _currentEmotion;

        string _currentSong;

        EmotionAPI _emotionAPI;

        RestClient client = new RestClient("https://croozio.azurewebsites.net/");

        private bool IsThereAnAppToTakePictures()
        {
            Intent intent = new Intent(MediaStore.ActionImageCapture);
            IList<ResolveInfo> availableActivities =
                PackageManager.QueryIntentActivities(intent, PackageInfoFlags.MatchDefaultOnly);
            return availableActivities != null && availableActivities.Count > 0;
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Get UUID
            _deviceID = "undefined";
            var telephonyDeviceID = string.Empty;
            var telephonySIMSerialNumber = string.Empty;
            TelephonyManager telephonyManager = (TelephonyManager)this.ApplicationContext.GetSystemService(Context.TelephonyService);
            if (telephonyManager != null)
            {
                if (!string.IsNullOrEmpty(telephonyManager.DeviceId))
                    telephonyDeviceID = telephonyManager.DeviceId;
                    _deviceID = telephonyDeviceID;
                //if (!string.IsNullOrEmpty(telephonyManager.SimSerialNumber))
                //    telephonySIMSerialNumber = telephonyManager.SimSerialNumber;
            }
            //var androidID = Android.Provider.Settings.Secure.GetString(this.ApplicationContext.ContentResolver, Android.Provider.Settings.Secure.AndroidId);
            //var deviceUuid = new UUID(androidID.GetHashCode(), ((long)telephonyDeviceID.GetHashCode() << 32) | telephonySIMSerialNumber.GetHashCode());
            //var deviceID = deviceUuid.ToString();

            // Generate session string using time
            _currentSession = DateTime.Now.ToString();

            // Send to database
            try
            {
                var request = new RestRequest("api/users", Method.POST);
                request.RequestFormat = DataFormat.Json;
                var body = new User
                {
                    id = _deviceID,
                    currentSession = _currentSession,
                    name = "Edwin Tsang"
                };
                request.AddBody(body);

                client.ExecuteAsync(request, response => {
                    System.Console.WriteLine(response.Content);
                });
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
            }


            photoTimer = new System.Timers.Timer(5000);
            photoTimer.Elapsed += async (sender, e) => await TakePhoto();
            photoTimer.Start();

            SetContentView(Resource.Layout.Main);

            _textureView = FindViewById<TextureView>(Resource.Id.textureView1);
            _textureView.SurfaceTextureListener = this;

            //_pictureButton = FindViewById<Button>(Resource.Id.GetPictureButton);
            //_pictureButton.Click += OnActionClick;

            //_imageView = FindViewById<ImageView>(Resource.Id.imageView1);

            _resultTextView = FindViewById<TextView>(Resource.Id.resultText);

            _emotionDetailsTextView = FindViewById<TextView>(Resource.Id.emotionDetails_text);

            _locationText = FindViewById<TextView>(Resource.Id.location_text);

            // Start Mediaplayer
            _player = MediaPlayer.Create(this,Resource.Raw.neutral);

            // Start Geolocation
            InitializeLocationManager();

            // Start Emotion API
            _emotionAPI = new EmotionAPI();


        }

        void InitializeLocationManager()
        {
            _locationManager = (LocationManager)GetSystemService(LocationService);
            Criteria criteriaForLocationService = new Criteria
            {
                Accuracy = Accuracy.Fine
            };
            IList<string> acceptableLocationProviders = _locationManager.GetProviders(criteriaForLocationService, true);

            if (acceptableLocationProviders.Any())
            {
                _locationProvider = acceptableLocationProviders.First();
            }
            else
            {
                _locationProvider = string.Empty;
            }
        }

        private async Task TakePhoto()
        {
            _camera.TakePicture(this, null, this);
        }

        public void OnSurfaceTextureAvailable(
       Android.Graphics.SurfaceTexture surface, int w, int h)
        {
            _camera = Android.Hardware.Camera.Open(1);


            _textureView.LayoutParameters =
                   new FrameLayout.LayoutParams(w, h);

            try
            {
                _camera.SetDisplayOrientation(90);
                _camera.SetPreviewTexture(surface);
                _camera.StartPreview();

            }
            catch (Java.IO.IOException ex)
            {
                System.Console.WriteLine(ex.Message);
            }
        }

        public bool OnSurfaceTextureDestroyed(
               Android.Graphics.SurfaceTexture surface)
        {
            _camera.StopPreview();
            _camera.Release();

            return true;
        }
        
        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {
            //throw new NotImplementedException();
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {
            //throw new NotImplementedException();
        }

        public void OnShutter()
        {
            //throw new NotImplementedException();
        }

        public async void OnPictureTaken(byte[] data, Android.Hardware.Camera camera)
        {
            //throw new NotImplementedException();
            System.Console.WriteLine("Photo taken");

            camera.StartPreview();

            var timestamp = new DateTime();

            //Get the bitmap with the right rotation
            _bitmap = BitmapHelpers.GetBitmap(data);

            //Resize the picture to be under 4MB (Emotion API limitation and better for Android memory)
            _bitmap = Bitmap.CreateScaledBitmap(_bitmap, 2000, (int)(2000 * _bitmap.Height / _bitmap.Width), false);

            //Display the image
            //_imageView.SetImageBitmap(_bitmap);

            using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
            {
                // Mood
                var currentMood = new Mood();

                //Get a stream
                _bitmap.Compress(Bitmap.CompressFormat.Jpeg, 90, stream);
                stream.Seek(0, System.IO.SeekOrigin.Begin);

                //Get and display the happiness score
                try
                {
                    var currentEmotion = await _emotionAPI.GetEmotion(stream);
                    currentMood = _emotionAPI.GetMood(currentEmotion);
                    var currentEmotionText = currentEmotion.Scores.ToRankedList().First().Key;
                    _resultTextView.Text = currentEmotionText;
                    _emotionDetailsTextView.Text = string.Format("Surprise: {0:f2} Happy: {1:f2} Neutral: {2:f2} Sad: {3:f2} Angry: {4:f2}", currentMood.surprise, currentMood.happiness, currentMood.neutral, currentMood.sadness, currentMood.anger);

                    // Check if emotion has changed before storing current
                    if (currentEmotionText!=_currentEmotion)
                    {
                        // Play music
                        int song;

                        switch (currentEmotionText)
                        {
                            case "Happiness":
                                song = Resource.Raw.happy;
                                _currentSong = "Happy - Pharrell Williams";
                                break;
                            case "Sadness":
                                song = Resource.Raw.sad;
                                _currentSong = "My Heart Will Go On - Celine Dione";
                                break;
                            case "Anger":
                                song = Resource.Raw.angry;
                                _currentSong = "Down With The Sickness - Disturbed";
                                break;
                            default:
                                song = Resource.Raw.neutral;
                                _currentSong = "Hey Brother - Avicii";
                                break;
                        }

                        _player.Reset();
                        _player.Release();
                        _player = MediaPlayer.Create(this, song);
                        _player.Start();
                    }

                    _currentEmotion = currentEmotionText;

                    try
                    {
                        var request = new RestRequest("api/data", Method.POST);
                        request.RequestFormat = DataFormat.Json;
                        var body = new DataPacket
                        {
                            userId = _deviceID,
                            tripId = _currentSession,
                            geo = new Geolocation
                            {
                                lat = _currentLocation.Latitude,
                                lon = _currentLocation.Longitude
                            },
                            mood = currentMood,
                            song = "Jingle Bells",
                            speed = _currentLocation.Speed,
                            time = timestamp
                        };
                        request.AddBody(body);

                        client.ExecuteAsync(request, response => {
                            System.Console.WriteLine(response.Content);
                        });
                    }
                    catch (Exception e)
                    {
                        System.Console.WriteLine(e.Message);
                    }
                }
                catch (Exception e)
                {
                    _emotionDetailsTextView.Text = "";
                    _resultTextView.Text = "No face detected";
                }
                
            }
        }

        public async void OnLocationChanged(Location location)
        {
            _currentLocation = location;
            if (_currentLocation == null)
            {
                _locationText.Text = "Unable to determine your location. Try again in a short while.";
            }
            else
            {
                _locationText.Text = string.Format("{0:f6},{1:f6},{2:f6}", _currentLocation.Latitude, _currentLocation.Longitude,_currentLocation.Speed);
            }
        }

        public void OnProviderDisabled(string provider)
        {
            //throw new NotImplementedException();
        }

        public void OnProviderEnabled(string provider)
        {
            //throw new NotImplementedException();
        }

        public void OnStatusChanged(string provider, [GeneratedEnum] Availability status, Bundle extras)
        {
            //throw new NotImplementedException();
        }

        protected override void OnResume()
        {
            base.OnResume();
            _locationManager.RequestLocationUpdates(_locationProvider, 0, 0, this);
            _currentLocation = _locationManager.GetLastKnownLocation(_locationProvider);
        }

        protected override void OnPause()
        {
            base.OnPause();
            _locationManager.RemoveUpdates(this);
        }
    }
}

