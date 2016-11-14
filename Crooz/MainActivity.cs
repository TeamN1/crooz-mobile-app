﻿using System;
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

namespace Crooz
{
    [Activity(Label = "Crooz", MainLauncher = true, Icon = "@drawable/icon", ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : Activity, TextureView.ISurfaceTextureListener, Android.Hardware.Camera.IShutterCallback, Android.Hardware.Camera.IPictureCallback
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
        Timer photoTimer;

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

            photoTimer = new Timer(5000);
            photoTimer.Elapsed += async (sender, e) => await TakePhoto();
            photoTimer.Start();

            SetContentView(Resource.Layout.Main);

            _textureView = FindViewById<TextureView>(Resource.Id.textureView1);
            _textureView.SurfaceTextureListener = this;

            _pictureButton = FindViewById<Button>(Resource.Id.GetPictureButton);
            _pictureButton.Click += OnActionClick;

            _imageView = FindViewById<ImageView>(Resource.Id.imageView1);

            _resultTextView = FindViewById<TextView>(Resource.Id.resultText);



            //if (IsThereAnAppToTakePictures())
            //{
            //    CreateDirectoryForPictures();

            //    _pictureButton = FindViewById<Button>(Resource.Id.GetPictureButton);
            //    _pictureButton.Click += OnActionClick;

            //    _imageView = FindViewById<ImageView>(Resource.Id.imageView1);

            //    _resultTextView = FindViewById<TextView>(Resource.Id.resultText);
            //}
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

            //Get the bitmap with the right rotation
            _bitmap = BitmapHelpers.GetBitmap(data);

            //Resize the picture to be under 4MB (Emotion API limitation and better for Android memory)
            _bitmap = Bitmap.CreateScaledBitmap(_bitmap, 2000, (int)(2000 * _bitmap.Height / _bitmap.Width), false);

            //Display the image
            _imageView.SetImageBitmap(_bitmap);

            using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
            {
                //Get a stream
                _bitmap.Compress(Bitmap.CompressFormat.Jpeg, 90, stream);
                stream.Seek(0, System.IO.SeekOrigin.Begin);

                //Get and display the happiness score
                try
                {
                    float result = await Core.GetAverageHappinessScore(stream);
                    System.Console.WriteLine(result);
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("error no face");
                }
                
            }
        }
    }
}

