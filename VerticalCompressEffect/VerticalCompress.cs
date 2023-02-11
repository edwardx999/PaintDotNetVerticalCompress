using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Reflection;
using System.Drawing.Text;
using System.Windows.Forms;
using System.IO.Compression;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Registry = Microsoft.Win32.Registry;
using RegistryKey = Microsoft.Win32.RegistryKey;
using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.AppModel;
using PaintDotNet.Clipboard;
using PaintDotNet.IndirectUI;
using PaintDotNet.Collections;
using PaintDotNet.PropertySystem;
using IntSliderControl = System.Int32;
using CheckboxControl = System.Boolean;
using ColorWheelControl = PaintDotNet.ColorBgra;
using AngleControl = System.Double;
using PanSliderControl = PaintDotNet.Pair<double, double>;
using TextboxControl = System.String;
using FilenameControl = System.String;
using DoubleSliderControl = System.Double;
using ListBoxControl = System.Byte;
using RadioButtonControl = System.Byte;
using ReseedButtonControl = System.Byte;
using MultiLineTextboxControl = System.String;
using RollControl = System.Tuple<double, double, double>;

[assembly: AssemblyTitle("Vertical Compress plugin for Paint.NET")]
[assembly: AssemblyDescription("Vertical Compress selected pixels")]
[assembly: AssemblyConfiguration("vertical compress")]
[assembly: AssemblyCompany("Edward Xie")]
[assembly: AssemblyProduct("Vertical Compress")]
[assembly: AssemblyCopyright("Copyright ©2023 by Edward Xie")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("1.0.*")]

namespace VerticalCompressEffect
{
	public class PluginSupportInfo : IPluginSupportInfo
	{
		public string Author {
			get {
				return base.GetType().Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright;
			}
		}
		public string Copyright {
			get {
				return base.GetType().Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
			}
		}

		public string DisplayName {
			get {
				return base.GetType().Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product;
			}
		}

		public Version Version {
			get {
				return base.GetType().Assembly.GetName().Version;
			}
		}

		public Uri WebsiteUri {
			get {
				return new Uri("https://www.getpaint.net/redirect/plugins.html");
			}
		}
	}



	[PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "Vertical Compress")]
	public class VerticalCompressEffectPlugin : PropertyBasedEffect
	{
		public static string StaticName {
			get {
				return "Vertical Compress";
			}
		}

		public static Image StaticIcon {
			get {
				return null;
			}
		}

		public static string SubmenuName {
			get {
				return null;
			}
		}

		public VerticalCompressEffectPlugin()
			: base(StaticName, StaticIcon, SubmenuName, new EffectOptions() { Flags = EffectFlags.Configurable, RenderingSchedule = EffectRenderingSchedule.None })
		{
		}

		public enum PropertyNames
		{
			MinVertSpace,
			MinHorizProt,
			MaxVertProt,
			Bg,
			MinHorizontalSpace,
			StaffLineLength,
			MinStaffSeparation,
			Debug
		}


		protected override PropertyCollection OnCreatePropertyCollection()
		{
			List<Property> props = new List<Property>();

			props.Add(new Int32Property(PropertyNames.MinVertSpace, 50, 1, 1000));
			props.Add(new Int32Property(PropertyNames.MinHorizProt, 10, 1, 1000));
			props.Add(new Int32Property(PropertyNames.MaxVertProt, 70, 1, 1000));
			props.Add(new Int32Property(PropertyNames.Bg, 254, 0, 255));
			props.Add(new Int32Property(PropertyNames.MinHorizontalSpace, 250, 1, 1000));
			props.Add(new Int32Property(PropertyNames.StaffLineLength, 3000, 0, 10000));
			props.Add(new Int32Property(PropertyNames.MinStaffSeparation, 100, 0, 1000));
			props.Add(new BooleanProperty(PropertyNames.Debug, false));

			return new PropertyCollection(props);
		}

		protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
		{
			ControlInfo configUI = CreateDefaultConfigUI(props);

			configUI.SetPropertyControlValue(PropertyNames.MinVertSpace, ControlInfoPropertyNames.DisplayName, "Minimum Vertical Space");
			configUI.SetPropertyControlValue(PropertyNames.MinHorizProt, ControlInfoPropertyNames.DisplayName, "Minimum Horizontal Protection");
			configUI.SetPropertyControlValue(PropertyNames.MaxVertProt, ControlInfoPropertyNames.DisplayName, "Maximum Vertical Protection");
			configUI.SetPropertyControlValue(PropertyNames.Bg, ControlInfoPropertyNames.DisplayName, "Background");
			configUI.SetPropertyControlValue(PropertyNames.MinHorizontalSpace, ControlInfoPropertyNames.DisplayName, "Minimum Horizontal Space");
			configUI.SetPropertyControlValue(PropertyNames.StaffLineLength, ControlInfoPropertyNames.DisplayName, "Staff Line Length");
			configUI.SetPropertyControlValue(PropertyNames.MinStaffSeparation, ControlInfoPropertyNames.DisplayName, "Minimum Staff Separation");
			configUI.SetPropertyControlValue(PropertyNames.Debug, ControlInfoPropertyNames.DisplayName, "Debug");

			return configUI;
		}

		protected override void OnCustomizeConfigUIWindowProperties(PropertyCollection props)
		{
			// Change the effect's window title
			props[ControlInfoPropertyNames.WindowTitle].Value = "Vertical Compress";
			// Add help button to effect UI
			props[ControlInfoPropertyNames.WindowHelpContentType].Value = WindowHelpContentType.PlainText;
			props[ControlInfoPropertyNames.WindowHelpContent].Value = "Vertical Compress v1.0\nCopyright ©2023 by Edward Xie\nAll rights reserved.";
			base.OnCustomizeConfigUIWindowProperties(props);
		}

		protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken, RenderArgs dstArgs, RenderArgs srcArgs)
		{
			MinVertSpace = newToken.GetProperty<Int32Property>(PropertyNames.MinVertSpace).Value;
			MinHorizProt = newToken.GetProperty<Int32Property>(PropertyNames.MinHorizProt).Value;
			MaxVertProt = newToken.GetProperty<Int32Property>(PropertyNames.MaxVertProt).Value;
			Bg = newToken.GetProperty<Int32Property>(PropertyNames.Bg).Value;
			MinHorizSpace = newToken.GetProperty<Int32Property>(PropertyNames.MinHorizontalSpace).Value;
			StaffLineLength = newToken.GetProperty<Int32Property>(PropertyNames.StaffLineLength).Value;
			MinStaffSeparation = newToken.GetProperty<Int32Property>(PropertyNames.MinStaffSeparation).Value;
			Debug = newToken.GetProperty<BooleanProperty>(PropertyNames.Debug).Value;

			base.OnSetRenderInfo(newToken, dstArgs, srcArgs);
		}

		protected override unsafe void OnRender(Rectangle[] rois, int startIndex, int length)
		{
			if (length == 0) return;
			for (int i = startIndex; i < startIndex + length; ++i)
			{
				Render(DstArgs.Surface, SrcArgs.Surface, rois[i]);
			}
		}

		#region User Entered Code
		// Name: Vertical Compress
		// Author: Edward Xie
		// Title: Vertical Compress
		#region UICode
		IntSliderControl MinVertSpace = 50; // [0,1000] Minimum Vertical Space
		IntSliderControl MinHorizProt = 10; // [0,1000] Minimum Horizontal Protection
		IntSliderControl MaxVertProt = 70; // [0,1000] Maximum Vertical Protection
		IntSliderControl Bg = 254; // [0,255] Background
		IntSliderControl MinHorizSpace = 350; // [0,1000] Minimum Horizontal Space
		IntSliderControl StaffLineLength = 3000; // [0,10000] Staff Line Length
		IntSliderControl MinStaffSeparation = 100; // [0,1000] Minimum Staff Separation
		bool Debug = false;
		#endregion

		const int stricken = -2;
		const int safe = -1;
		const int notSafe = 0;
		const int candidatePath = 1;
		const int realPath = 2;
		const int notDeadEnd = 3;

		void Render(Surface dst, Surface src, Rectangle _rect)
		{
			// Delete any of these lines you don't need
			Rectangle selection = EnvironmentParameters.GetSelection(src.Bounds).GetBoundsInt();
			const int dimLim = short.MaxValue;
			if (selection.Height < Math.Max(3, MinVertSpace) || selection.Width < Math.Max(3, MinHorizSpace) || selection.Width > dimLim || selection.Height > dimLim)
			{
				return;
			}
			#region Finding Largest Cluster
			var ranges = FindRanges(src, selection, 0, (byte)Bg);
			if (IsCancelRequested) return;
			var clusters = ClusterRanges(ranges);
			if (clusters.Count == 0)
			{
				return;
			}
			if (IsCancelRequested) return;
			var largestCluster = clusters[0];
			int largestClusterSize = largestCluster.NumPixels;
			for (int i = 1; i < clusters.Count; ++i)
			{
				var cluster = clusters[i];
				int clusterSize = cluster.NumPixels;
				if (clusterSize > largestClusterSize)
				{
					largestCluster = cluster;
					largestClusterSize = clusterSize;
				}
			}
			#endregion
			var pixels = new List<Point>();
			foreach (var rect in largestCluster.Ranges)
			{
				for (int y = rect.Top; y < rect.Bottom; ++y)
				{
					for (int x = rect.Left; x < rect.Right; ++x)
					{
						pixels.Add(new Point(x, y));
					}
				}
			}
			// Arrays are row-column (y-x). Surface is x-y.
			if (IsCancelRequested) return;
			var pathCounts = new short[src.Height, src.Width]; // could be made smaller than Src, same to make math easier
			#region Finding Horizontal Paths
			{
				pixels.Sort((p1, p2) =>
				{
					int r = p1.X.CompareTo(p2.X);
					return r != 0 ? r : p1.Y.CompareTo(p2.Y);
				});
				int iPixIdx = 0;
				for (; iPixIdx != pixels.Count; ++iPixIdx)
				{
					var pixel = pixels[iPixIdx];
					if (pixel.X > selection.Left)
					{
						break;
					}
					pathCounts[pixel.Y, pixel.X] = 1;
				}
				if (IsCancelRequested) return;
				for (; iPixIdx != pixels.Count; ++iPixIdx)
				{
					var pixel = pixels[iPixIdx];
					short pathCount;
					if (pixel.Y <= selection.Top)
					{
						pathCount = Math.Max(pathCounts[pixel.Y - 1, pixel.X - 1], pathCounts[pixel.Y, pixel.X - 1]);
					}
					else if (pixel.Y >= selection.Height - 1)
					{
						pathCount = Math.Max(pathCounts[pixel.Y, pixel.X - 1], pathCounts[pixel.Y + 1, pixel.X - 1]);
					}
					else
					{
						pathCount = Math.Max(pathCounts[pixel.Y, pixel.X - 1], Math.Max(pathCounts[pixel.Y, pixel.X - 1], pathCounts[pixel.Y + 1, pixel.X - 1]));
					}
					pathCounts[pixel.Y, pixel.X] = (short)(pathCount + 1);
				}
				if (IsCancelRequested) return;
				for (iPixIdx = pixels.Count; iPixIdx-- > 0;)
				{
					var pixel = pixels[iPixIdx];
					if (pixel.X >= selection.Right - 1)
					{
						continue;
					}
					short upper = pixel.Y <= selection.Top ? (short)0 : pathCounts[pixel.Y - 1, pixel.X + 1];
					short middle = pathCounts[pixel.Y, pixel.X + 1];
					short lower = pixel.Y >= selection.Bottom - 1 ? (short)0 : pathCounts[pixel.Y + 1, pixel.X + 1];
					short self = pathCounts[pixel.Y, pixel.X];
					pathCounts[pixel.Y, pixel.X] = Math.Max(upper, Math.Max(middle, Math.Max(lower, self)));
				}
				if (IsCancelRequested) return;
				pixels.RemoveAll(pixel =>
				{
					if (pathCounts[pixel.Y, pixel.X] >= MinHorizProt)
					{
						pathCounts[pixel.Y, pixel.X] = safe;
						return true;
					}
					pathCounts[pixel.Y, pixel.X] = notSafe;
					return false;
				});
			}
			#endregion
			if (IsCancelRequested) return;
			#region Protecting Small Clusters
			foreach (var cluster in clusters)
			{
				if (cluster != largestCluster)
				{
					foreach (var rect in cluster.Ranges)
					{
						for (int y = rect.Top; y < rect.Bottom; ++y)
						{
							for (int x = rect.Left; x < rect.Right; ++x)
							{
								pathCounts[y, x] = safe;
							}
						}
					}
				}
			}
			#endregion
			if (IsCancelRequested) return;
			#region Finding Vertical Paths
			{
				pixels.Sort((p1, p2) =>
				{
					int r = p1.Y.CompareTo(p2.Y);
					return r != 0 ? r : p1.X.CompareTo(p2.X);
				});
				if (IsCancelRequested) return;
				int iPixIdx = 0;
				for (; iPixIdx != pixels.Count; ++iPixIdx)
				{
					var pixel = pixels[iPixIdx];
					if (pixel.Y > selection.Top)
					{
						break;
					}
					pathCounts[pixel.Y, pixel.X] = 1;
				}
				if (IsCancelRequested) return;
				for (; iPixIdx != pixels.Count; ++iPixIdx)
				{
					var pixel = pixels[iPixIdx];
					short left = pixel.X <= selection.Left ? (short)0 : pathCounts[pixel.Y - 1, pixel.X - 1];
					short middle = pathCounts[pixel.Y - 1, pixel.X];
					short right = pixel.X >= selection.Right - 1 ? (short)0 : pathCounts[pixel.Y - 1, pixel.X + 1];
					pathCounts[pixel.Y, pixel.X] = (short)(Math.Max((short)0, Math.Max(left, Math.Max(middle, right))) + 1);
				}
				if (IsCancelRequested) return;
				for (iPixIdx = pixels.Count; iPixIdx-- > 0;)
				{
					var pixel = pixels[iPixIdx];
					if (pixel.Y >= selection.Bottom - 1)
					{
						continue;
					}
					short left = pixel.X <= selection.Left ? (short)0 : pathCounts[pixel.Y + 1, pixel.X - 1];
					short middle = pathCounts[pixel.Y - 1, pixel.X];
					short right = pixel.X >= selection.Right - 1 ? (short)0 : pathCounts[pixel.Y + 1, pixel.X + 1];
					short self = pathCounts[pixel.Y, pixel.X];
					pathCounts[pixel.Y, pixel.X] = (short)(Math.Max((short)0, Math.Max(left, Math.Max(middle, right))) + 1);
				}
				if (IsCancelRequested) return;
				foreach (var pixel in pixels)
				{
					if (pathCounts[pixel.Y, pixel.X] <= MaxVertProt)
					{
						pathCounts[pixel.Y, pixel.X] = safe;
					}
					else
					{
						pathCounts[pixel.Y, pixel.X] = notSafe;
					}
				}
			}
			#endregion
			if (IsCancelRequested) return;
			#region Marking Safe Pixels Due to MinVertSpace and MinHorizSpace
			short[,] safePoints; // transposed x, y
			{
				int tailSpace = MinVertSpace / 2;
				int headSpace = MinVertSpace - tailSpace;
				for (int y = selection.Top; y < selection.Top + headSpace; ++y)
				{
					for (int x = selection.Left; x < selection.Right; ++x)
					{
						pathCounts[y, x] = safe;
					}
				}
				if (IsCancelRequested) return;
				for (int y = selection.Bottom - tailSpace; y < selection.Bottom; ++y)
				{
					for (int x = selection.Left; x < selection.Right; ++x)
					{
						pathCounts[y, x] = safe;
					}
				}
				if (IsCancelRequested) return;
				var safePointsPass1 = getFatten(pathCounts, MinHorizSpace - MinHorizSpace / 2);
				if (IsCancelRequested) return;
				safePoints = getFatten(transpose(safePointsPass1), MinVertSpace - MinVertSpace / 2);
			}
			#endregion
			if (IsCancelRequested) return;
			#region Remove Removable Dead Ends
			{
				var seeds = new List<int>();
				int x = selection.Right - 1;
				for (int y = selection.Top; y < selection.Bottom; ++y)
				{
					if (safePoints[x, y] == notSafe)
					{
						safePoints[x, y] = notDeadEnd;
						seeds.Add(y);
					}
				}
				if (IsCancelRequested) return;
				while (x-- > 0)
				{
					var tempSeeds = new List<int>();
					foreach (int yBegin in seeds)
					{
						for (int y = yBegin; safePoints[x, y] == notSafe; --y)
						{
							safePoints[x, y] = notDeadEnd;
							tempSeeds.Add(y);
							if (y == selection.Top)
							{
								break;
							}
						}
						for (int y = yBegin + 1; y < selection.Bottom && safePoints[x, y] == notSafe; ++y)
						{
							safePoints[x, y] = notDeadEnd;
							tempSeeds.Add(y);
						}
					}
					seeds = tempSeeds;
					if (IsCancelRequested) return;
				}
			}
			#endregion
			var paths = new Dictionary<short, int[]>();
			var pathSources = new short[safePoints.GetLength(0), safePoints.GetLength(1)];
			#region Trace Removable Paths
			{
				for (int y = selection.Top; y < selection.Bottom; ++y)
				{
					var path = traceDownPath(selection, safePoints, pathSources, y);
					if (path != null)
					{
						paths.Add((short)y, path);
					}
					if (IsCancelRequested) return;
				}
			}
			#endregion
			#region Remove Paths That Would Push Staves Too Close
			if (StaffLineLength > 0)
			{
				var staffLines = new bool[src.Width, src.Height];
				for (int y = selection.Top; y < selection.Bottom; ++y)
				{
					var staffLine = new bool[src.Width];
					int count = 0;
					for (int x = selection.Left; x < selection.Right; ++x)
					{
						if (Brightness(src[x, y]) <= Bg)
						{
							++count;
							staffLine[x] = true;
						}
					}
					if (count >= StaffLineLength)
					{
						for (int x = 0; x < src.Width; ++x)
						{
							staffLines[x, y] = staffLine[x];
						}
					}
					if (IsCancelRequested) return;
				}
				for (int x = selection.Left; x < selection.Right; ++x)
				{
					int previousHit = -1;
					for (int y = selection.Top; y < selection.Bottom; ++y)
					{
						if (staffLines[x, y])
						{
							if (previousHit != -1)
							{
								int dist = y - previousHit;
								if (dist <= MinStaffSeparation)
								{
									for (int y2 = previousHit; y2 < y; ++y2)
									{
										if (safePoints[x, y2] == realPath)
										{
											removePath(selection, safePoints, pathSources, paths, x, y2);
										}
									}
								}
								else
								{
									int freeSpace = dist - MinStaffSeparation;
									var freeCoords = new List<int>();
									for (int y2 = previousHit + 1; y2 < y; ++y2)
									{
										if (safePoints[x, y2] == realPath)
										{
											freeCoords.Add(y2);
										}
									}
									int unsafeCount = freeCoords.Count;
									int i = 0;
									for (; unsafeCount > freeSpace && i < freeCoords.Count; --unsafeCount, ++i)
									{
										removePath(selection, safePoints, pathSources, paths, x, freeCoords[i]);
									}
								}
							}
							previousHit = y;
						}
					}
					if (IsCancelRequested) return;
				}
			}
			#endregion
			if (Debug)
			{
				for (int x = selection.Left; x < selection.Right; ++x)
				{
					for (int y = selection.Top; y < selection.Bottom; ++y)
					{
						var c = new ColorBgra();
						c.A = 255;
						var v = safePoints[x, y];
						if (v < 0)
						{
							c.B = c.G = c.R = (byte)(256 + v * 20);
						}
						else
						{
							c.B = c.G = c.R = (byte)(v * 50);
						}
						dst[x, y] = c;
					}
				}
				return;
			}
			#region Remove Paths
			for (int x = selection.Left; x < selection.Right; ++x)
			{
				int writeHead = selection.Top;
				for (int readHead = selection.Top; readHead < selection.Bottom; ++readHead)
				{
					if (safePoints[x, readHead] != realPath)
					{
						dst[x, writeHead] = src[x, readHead];
						++writeHead;
					}
				}
				for (; writeHead < selection.Bottom; ++writeHead)
				{
					dst[x, writeHead] = ColorBgra.White;
				}
				if (IsCancelRequested) return;
			}
			#endregion
		}

		int[] traceDownPath(Rectangle selection, short[,] safePoints, short[,] pathSources, int y)
		{
			int x = selection.Left;
			if (safePoints[x, y] != notDeadEnd)
			{
				return null;
			}
			var path = new int[safePoints.GetLength(0)];
			safePoints[x, y] = candidatePath;
			path[x] = y;
			int startY = y;
			++x;
			for (; x < selection.Right; ++x)
			{
				int highest = y;
				bool foundAbove = false;
				// Look above for highest free pixel. We know y contacts the previous path so we go up until we hit a non-free pixel.
				while (true)
				{
					if (safePoints[x, highest] != notDeadEnd)
					{
						++highest;
						break;
					}
					foundAbove = true;
					--highest;
				}
				if (!foundAbove) // If we couldn't find one above, we look below for a free pixel, checking that we aren't passing through non-free pixels.
				{
					// highest = y + 1 at this point
					while (true)
					{
						if (safePoints[x - 1, highest] != notDeadEnd) // We are at a dead end.
						{
							return null;
						}
						if (safePoints[x, highest] == notDeadEnd)
						{
							break;
						}
						++highest;
					}
				}
				safePoints[x, highest] = candidatePath;
				path[x] = highest;
				y = highest;
			}
			// Path finished, trace it back
			for (int pX = selection.Left; pX < selection.Right; ++pX)
			{
				safePoints[pX, path[pX]] = realPath;
			}
			for (int pX = selection.Left; pX < selection.Right; ++pX)
			{
				pathSources[pX, path[pX]] = (short)startY;
			}
			return path;
		}

		void removePath(Rectangle selection, short[,] safePoints, short[,] pathSources, IDictionary<short, int[]> paths, int x, int y)
		{
			short source = pathSources[x, y];
			var path = paths[source];
			paths.Remove(source);
			for (int x2 = selection.Left; x2 < selection.Right; ++x2)
			{
				safePoints[x2, path[x2]] = stricken;
			}
		}

		#region Cluster Code
		static byte Brightness(ColorBgra c)
		{
			return (byte)(Math.Round(((float)c.R + c.G + c.B) / 3.0f));
		}
		static List<Rectangle> FindRanges(Surface src, Rectangle rect, byte lower, byte upper)
		{
			List<Rectangle> ranges = new List<Rectangle>();
			byte rangeFound = 0;
			int rangeStart = 0, rangeEnd = 0;
			for (int y = rect.Top; y < rect.Bottom; ++y)
			{
				for (int x = rect.Left; x < rect.Right; ++x)
				{
					switch (rangeFound)
					{
						case 0:
							{
								var b = Brightness(src[x, y]);
								if (b >= lower && b <= upper)
								{
									rangeFound = 1;
									rangeStart = x;
								}
								break;
							}
						case 1:
							{
								var b = Brightness(src[x, y]);
								if (b < lower || b > upper)
								{
									rangeFound = 2;
									rangeEnd = x;
									goto case 2;
								}
								break;
							}
						case 2:
							{
								ranges.Add(new Rectangle(rangeStart, y, rangeEnd - rangeStart, 1));
								rangeFound = 0;
								break;
							}
					}
				}
				if (1 == rangeFound)
				{
					ranges.Add(new Rectangle(rangeStart, y, rect.Right - rangeStart, 1));
					rangeFound = 0;
				}
			}
			CompressRanges(ranges);
			return ranges;
		}

		static void CompressRanges(List<Rectangle> ranges)
		{
			ranges.Sort((a, b) =>
			{
				if (a.Left < b.Left)
				{
					return -1;
				}
				if (a.Left > b.Left)
				{
					return 1;
				}
				return a.Top - b.Top;
			});
			for (int i = 1; i < ranges.Count(); ++i)
			{
				if (ranges[i - 1].Left == ranges[i].Left && ranges[i - 1].Right == ranges[i].Right && ranges[i - 1].Bottom == ranges[i].Top)
				{
					ranges[i - 1] = new Rectangle(ranges[i - 1].Location, new Size(ranges[i].Width, ranges[i - 1].Height + ranges[i].Height));
					ranges.RemoveAt(i--);
				}
			}
		}


		static bool OverlapsX(Rectangle a, Rectangle b)
		{
			return (a.Left <= b.Right) && (a.Right >= b.Left);
		}

		List<Cluster> ClusterRanges(List<Rectangle> ranges)
		{
			List<ClusterTestNode> tests = new List<ClusterTestNode>();
			ranges.ForEach(rr =>
			{
				ClusterPart toAdd = new ClusterPart(rr);
				tests.Add(new ClusterTestNode(toAdd, true));
				tests.Add(new ClusterTestNode(toAdd, false));
			});
			tests.Sort((a, b) =>
			{ //sort by y, if ys are same, bottoms are first
				int ydif = a.Y - b.Y;
				if (0 != ydif) return ydif;
				return a.IsTop.CompareTo(b.IsTop);
			});

			List<Cluster> Clusters = new List<Cluster>();
			Stack<int> searchStack = new Stack<int>();
			int max = tests.Count;
			for (int i = max - 1; i >= 0; --i)
			{
				ClusterTestNode CurrentNode = tests[i];
				if (!CurrentNode.IsTop) continue; //if it is a bottom or is already in a cluster, nothing is done
				if (CurrentNode.Parent.Cluster == null)
				{
					//Console.WriteLine("\nTesting "+CurrentNode.Parent.Rectangle);
					Cluster CurrentCluster = new Cluster();
					Clusters.Add(CurrentCluster);
					searchStack.Push(i);
					while (searchStack.Count > 0)
					{ //search for contacts
						int searchIndex = searchStack.Pop();
						ClusterTestNode SearchNode = tests[searchIndex];
						//Console.WriteLine("\tSearch Seed: "+SearchNode.Parent.Rectangle);
						if (SearchNode.Parent.Cluster != null) continue;
						SearchNode.Parent.Cluster = CurrentCluster;
						CurrentCluster.Ranges.Add(SearchNode.Parent.Rectangle);
						//search up for bottoms
						for (int s = searchIndex - 1; s >= 0; --s)
						{
							if (!tests[s].IsTop)
							{
								if (tests[s].Y == SearchNode.Parent.Rectangle.Top && OverlapsX(tests[s].Parent.Rectangle, SearchNode.Parent.Rectangle))
								{
									searchStack.Push(s);
								}
								else if (tests[s].Y < SearchNode.Parent.Rectangle.Top)
								{
									//Console.WriteLine("\t\tStopped at "+s);
									break;
								}
							}
						}
						//search down for tops
						for (int s = searchIndex + 1; s < max; ++s)
						{
							if (tests[s].IsTop)
							{
								if (tests[s].Y == SearchNode.Parent.Rectangle.Bottom && OverlapsX(tests[s].Parent.Rectangle, SearchNode.Parent.Rectangle))
								{
									searchStack.Push(s);
								}
								else if (tests[s].Y > SearchNode.Parent.Rectangle.Bottom)
								{
									//Console.WriteLine("\t\tStopped at "+s);
									break;
								}
							}
						}
					}
				}
			}
			return Clusters;
		}

		class ClusterPart
		{
			public Cluster Cluster = null;
			public readonly Rectangle Rectangle;
			public ClusterPart(Rectangle rr)
			{
				Rectangle = rr;
			}
		}

		class ClusterTestNode
		{
			public readonly ClusterPart Parent;
			public readonly bool IsTop;
			public readonly int Y;
			public ClusterTestNode(ClusterPart Parent, bool Top)
			{
				this.Parent = Parent;
				this.IsTop = Top;
				Y = Top ? Parent.Rectangle.Top : Parent.Rectangle.Bottom;
			}
		}

		struct ScanRange
		{
			public int left, right, y, direction;
			public ScanRange(int left, int right, int y, int direction)
			{
				this.left = left;
				this.right = right;
				this.y = y;
				this.direction = direction;
			}
		}

		class Cluster
		{
			public List<Rectangle> Ranges;

			public Cluster()
			{
				Ranges = new List<Rectangle>();
			}

			public int NumPixels {
				get {
					int sum = 0;
					foreach (Rectangle r in Ranges) sum += (r.Height * r.Width);
					return sum;
				}
			}

			public Rectangle Contains(Point p)
			{
				//if(!sorted) { ranges.Sort(); sorted=true; }
				foreach (Rectangle r in Ranges)
				{
					if (r.Contains(p)) { return r; }
				}
				return new Rectangle(0, 0, 0, 0);
			}

			public void CompressRanges()
			{
				VerticalCompressEffectPlugin.CompressRanges(Ranges);
			}
		}
		#endregion

		static Pair<int, short> minIndex(short[,] array, int row, int begin, int end)
		{
			int minIndex = begin;
			short minValue = array[row, begin];
			++begin;
			for (; begin < end; ++begin)
			{
				short value = array[row, begin];
				if (value < minValue)
				{
					minValue = value;
					minIndex = begin;
				}
			}
			return new Pair<int, short>(minIndex, minValue);
		}

		static short[,] getFatten(short[,] safePoints, int radius)
		{
			int width = safePoints.GetLength(1);
			int height = safePoints.GetLength(0);
			var ret = new short[height, width];
			if (radius == 0)
			{
				for (int y = 0; y < height; ++y)
				{
					for (int x = 0; x < width; ++x)
					{
						ret[y, x] = safePoints[y, x];
					}
				}
			}
			else if (width <= radius)
			{
				for (int y = 0; y < height; ++y)
				{
					short minValue = minIndex(safePoints, y, 0, width).Second;
					for (int x = 0; x < width; ++x)
					{
						ret[y, x] = minValue;
					}
				}
			}
			else
			{
				for (int y = 0; y < height; ++y)
				{
					var currentMinData = minIndex(safePoints, y, 0, radius);
					int currentMinIndex = currentMinData.First;
					short currentMin = currentMinData.Second;
					for (int x = 0; x < width; ++x)
					{
						int rangeBegin = x <= radius ? 0 : x - radius;
						int beforeRangeEnd = x + radius;
						int rangeEnd;
						if (beforeRangeEnd < width)
						{
							short value = safePoints[y, beforeRangeEnd];
							if (value <= currentMin)
							{
								currentMin = value;
								currentMinIndex = beforeRangeEnd;
							}
							rangeEnd = beforeRangeEnd + 1;
						}
						else
						{
							rangeEnd = width;
						}
						if (currentMinIndex < rangeBegin)
						{
							currentMinData = minIndex(safePoints, y, rangeBegin, rangeEnd);
							currentMinIndex = currentMinData.First;
							currentMin = currentMinData.Second;
						}
						ret[y, x] = currentMin;
					}
				}
			}
			return ret;
		}

		static short[,] transpose(short[,] arr)
		{
			int height = arr.GetLength(0);
			int width = arr.GetLength(1);
			var ret = new short[width, height];
			for (int y = 0; y < height; ++y)
			{
				for (int x = 0; x < width; ++x)
				{
					ret[x, y] = arr[y, x];
				}
			}
			return ret;
		}
		#endregion
	}
}
