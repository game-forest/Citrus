namespace Launcher
{
	partial class MainForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
			this.Logo = new System.Windows.Forms.PictureBox();
			((System.ComponentModel.ISupportInitialize)(this.Logo)).BeginInit();
			this.SuspendLayout();
			// 
			// Logo
			// 
			this.Logo.Image = global::Launcher.Properties.Resources.Splash;
			this.Logo.InitialImage = null;
			this.Logo.Location = new System.Drawing.Point(0, 0);
			this.Logo.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
			this.Logo.Name = "Logo";
			this.Logo.Size = new System.Drawing.Size(332, 512);
			this.Logo.TabIndex = 0;
			this.Logo.TabStop = false;
			this.Logo.UseWaitCursor = true;
			// 
			// MainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(332, 512);
			this.Controls.Add(this.Logo);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
			this.Name = "MainForm";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "MainForm";
			this.UseWaitCursor = true;
			((System.ComponentModel.ISupportInitialize)(this.Logo)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.PictureBox Logo;
	}
}
