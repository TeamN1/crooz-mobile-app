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


namespace Crooz
{
    [Activity(Label = "Crooz", MainLauncher = true, Icon = "@drawable/icon", ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : Activity, TextureView.ISurfaceTextureListener, Android.Hardware.Camera.IShutterCallback, Android.Hardware.Camera.IPictureCallback, ILocationListener
    {
        public static File _file;
        public static File _dir;
        public static Bitmap _bitmap;
        private ImageView _imageView;
        private Button _pictureButton;
        private TextView _resultTextView;
        private bool _isCaptureMode = true;
        Android.Hardware.Camera _camera;
        TextureView _textureView;
        System.Timers.Timer photoTimer;

        Location _currentLocation;
        LocationManager _locationManager;

        string _locationProvider;
        TextView _locationText;

        RestClient client = new RestClient("https://croozio.azurewebsites.net/");

        private void CreateDirectoryForPictures()
        {
            _dir = new File(
                Android.OS.Environment.GetExternalStoragePublicDirectory(
                    Android.OS.Environment.DirectoryPictures), "CameraAppDemo");
            if (!_dir.Exists())
            {
                _dir.Mkdirs();
            }
        }

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
            var deviceID = "undefined";
            var telephonyDeviceID = string.Empty;
            var telephonySIMSerialNumber = string.Empty;
            TelephonyManager telephonyManager = (TelephonyManager)this.ApplicationContext.GetSystemService(Context.TelephonyService);
            if (telephonyManager != null)
            {
                if (!string.IsNullOrEmpty(telephonyManager.DeviceId))
                    telephonyDeviceID = telephonyManager.DeviceId;
                    deviceID = telephonyDeviceID;
                //if (!string.IsNullOrEmpty(telephonyManager.SimSerialNumber))
                //    telephonySIMSerialNumber = telephonyManager.SimSerialNumber;
            }
            //var androidID = Android.Provider.Settings.Secure.GetString(this.ApplicationContext.ContentResolver, Android.Provider.Settings.Secure.AndroidId);
            //var deviceUuid = new UUID(androidID.GetHashCode(), ((long)telephonyDeviceID.GetHashCode() << 32) | telephonySIMSerialNumber.GetHashCode());
            //var deviceID = deviceUuid.ToString();

            photoTimer = new System.Timers.Timer(5000);
            photoTimer.Elapsed += async (sender, e) => await TakePhoto();
            photoTimer.Start();

            SetContentView(Resource.Layout.Main);

            _textureView = FindViewById<TextureView>(Resource.Id.textureView1);
            _textureView.SurfaceTextureListener = this;

            _pictureButton = FindViewById<Button>(Resource.Id.GetPictureButton);
            _pictureButton.Click += OnActionClick;

            _imageView = FindViewById<ImageView>(Resource.Id.imageView1);

            _resultTextView = FindViewById<TextView>(Resource.Id.resultText);

            _locationText = FindViewById<TextView>(Resource.Id.location_text);

            InitializeLocationManager();


            //if (IsThereAnAppToTakePictures())
            //{
            //    CreateDirectoryForPictures();

            //    _pictureButton = FindViewById<Button>(Resource.Id.GetPictureButton);
            //    _pictureButton.Click += OnActionClick;

            //    _imageView = FindViewById<ImageView>(Resource.Id.imageView1);

            //    _resultTextView = FindViewById<TextView>(Resource.Id.resultText);
            //}
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

        private void OnActionClick(object sender, EventArgs eventArgs)
        {
            if (_isCaptureMode == true)
            {
                Intent intent = new Intent(MediaStore.ActionImageCapture);
                _file = new Java.IO.File(_dir, String.Format("myPhoto_{0}.jpg", Guid.NewGuid()));
                intent.PutExtra(MediaStore.ExtraOutput, Android.Net.Uri.FromFile(_file));
                StartActivityForResult(intent, 0);
            }
            else
            {
                _imageView.SetImageBitmap(null);
                if (_bitmap != null)
                {
                    _bitmap.Recycle();
                    _bitmap.Dispose();
                    _bitmap = null;
                }
                _pictureButton.Text = "Take Picture";
                _resultTextView.Text = "";
                _isCaptureMode = true;
            }
        }

        protected override async void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            try
            {
                //Get the bitmap with the right rotation
                _bitmap = BitmapHelpers.GetAndRotateBitmap(_file.Path);

                //Resize the picture to be under 4MB (Emotion API limitation and better for Android memory)
                _bitmap = Bitmap.CreateScaledBitmap(_bitmap, 2000, (int)(2000 * _bitmap.Height / _bitmap.Width), false);

                //Display the image
                _imageView.SetImageBitmap(_bitmap);

                //Loading message
                _resultTextView.Text = "Loading...";

                using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
                {
                    //Get a stream
                    _bitmap.Compress(Bitmap.CompressFormat.Jpeg, 90, stream);
                    stream.Seek(0, System.IO.SeekOrigin.Begin);

                    //Get and display the happiness score
                    float result = await Core.GetAverageHappinessScore(stream);
                    _resultTextView.Text = Core.GetHappinessMessage(result);
                }
            }
            catch (Exception ex)
            {
                _resultTextView.Text = ex.Message;
            }
            finally
            {
                _pictureButton.Text = "Reset";
                _isCaptureMode = false;
            }
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
            _imageView.SetImageBitmap(_bitmap);

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
                    currentMood = await Core.GetMood(stream);
                    
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("error no face");
                }
                try
                {
                    var request = new RestRequest("api/data", Method.POST);
                    request.RequestFormat = DataFormat.Json;
                    var body = new DataPacket
                    {
                        userId = "test",
                        tripId = "room",
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

