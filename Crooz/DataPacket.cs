using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace Crooz
{
    class Geolocation
    {
        public double lat { get; set; }
        public double lon { get; set; }

    }

    class Mood
    {
        public double surprise { get; set; }
        public double happiness { get; set; }
        public double neutral { get; set; }
        public double sadness { get; set; }
        public double anger { get; set; }


    }
    class DataPacket
    {
        public string userId { get; set; }
        public string tripId { get; set; }
        public Geolocation geo { get; set; }
        public Mood mood { get; set; }
        public string song { get; set; }
        public double speed { get; set; }
        public DateTime time { get; set; }

    }
}