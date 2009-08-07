﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Tiling;

namespace BruTileDemo
{
  /// <summary>
  /// Interaction logic for Window1.xaml
  /// </summary>
  public partial class Window1 : Window
  {
    public Window1()
    {
      AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
      
      InitializeComponent();
      this.map.Loaded += new RoutedEventHandler(map_Loaded);
    }

    void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
      MessageBox.Show("An Unhandled exception occurred, the application will shut down", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    void map_Loaded(object sender, RoutedEventArgs e)
    {
      IConfig config = new ConfigOsm();
      map.RootLayer = new TileLayer(new WebTileProvider(config.RequestBuilder), config.TileSchema);
      //if you want to use caching to local file system for this layer use this line instead:
      //map.RootLayer = new TileLayer(new WebTileProvider(config.RequestBuilder), config.TileSchema, config.FileCache);

      FpsText.DataContext = map.FpsCounter;
      FpsText.SetBinding(TextBlock.TextProperty, new Binding("Fps"));
    }

    private void map_ErrorMessageChanged(object sender, EventArgs e)
    {
      this.Error.Text = map.ErrorMessage;
    }

    private void Osm_Click(object sender, RoutedEventArgs e)
    {
      IConfig config = new ConfigOsm();
      map.RootLayer = new TileLayer(new WebTileProvider(config.RequestBuilder), config.TileSchema);

      //todo: move elsewhere
      TileCountText.DataContext = map.RootLayer.MemoryCache;
      TileCountText.SetBinding(TextBlock.TextProperty, new Binding("TileCount"));
    }

    private void GeodanWms_Click(object sender, RoutedEventArgs e)
    {
      IConfig config = new ConfigWms();
      map.RootLayer = new TileLayer(new WebTileProvider(config.RequestBuilder), config.TileSchema);

      //todo: move elsewhere
      TileCountText.DataContext = map.RootLayer.MemoryCache;
      TileCountText.SetBinding(TextBlock.TextProperty, new Binding("TileCount"));
    }

    private void GeodanTms_Click(object sender, RoutedEventArgs e)
    {
      IConfig config = new ConfigTms();
      map.RootLayer = new TileLayer(new WebTileProvider(config.RequestBuilder), config.TileSchema);

      //todo: move elsewhere
      TileCountText.DataContext = map.RootLayer.MemoryCache;
      TileCountText.SetBinding(TextBlock.TextProperty, new Binding("TileCount"));

    }

    private void BingMaps_Click(object sender, RoutedEventArgs e)
    {
      IConfig config = new ConfigVE();
      map.RootLayer = new TileLayer(new WebTileProvider(config.RequestBuilder), config.TileSchema);

      //todo: move elsewhere
      TileCountText.DataContext = map.RootLayer.MemoryCache;
      TileCountText.SetBinding(TextBlock.TextProperty, new Binding("TileCount"));

    }

  }
}
