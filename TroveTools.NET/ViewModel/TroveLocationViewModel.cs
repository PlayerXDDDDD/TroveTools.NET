﻿using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TroveTools.NET.Framework;
using TroveTools.NET.Model;

namespace TroveTools.NET.ViewModel
{
    class TroveLocationViewModel : ViewModelBase<TroveLocation>
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private DelegateCommand _LaunchFolderCommand;

        public TroveLocationViewModel(TroveLocation dataObject) : base(dataObject) { }

        public TroveLocationViewModel(string locationName, string locationPath, bool enabled = true)
            : base(new TroveLocation(locationName, locationPath, enabled)) { }


        public DelegateCommand LaunchFolderCommand
        {
            get
            {
                if (_LaunchFolderCommand == null) _LaunchFolderCommand = new DelegateCommand(p => TroveModViewModel.LaunchPath(DataObject.LocationPath));
                return _LaunchFolderCommand;
            }
        }
    }
}