﻿using System.Drawing;

namespace DesktopMagicPluginAPI
{
    /// <summary>
    ///  Defines properties and methods that provide information about the main application.
    /// </summary>
    public interface IPluginData
    {
        /// <summary>
        /// Gets the font of the main application.
        /// </summary>
        string Font { get; }

        /// <summary>
        /// Gets the color of the main application.
        /// </summary>
        Color Color { get; }

        /// <summary>
        /// Gets the window size of the main application.
        /// </summary>
        Point WindowSize { get; }

        /// <summary>
        /// Gets the window position of the main application.
        /// </summary>
        Point WindowPosition { get; }

        /// <summary>
        /// Updates the plugin window
        /// </summary>
        void UpdateWindow();
    }
}