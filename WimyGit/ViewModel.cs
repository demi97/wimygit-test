﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WimyGit
{
	partial class ViewModel : System.ComponentModel.INotifyPropertyChanged, ILogger
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private RepositoryTab repository_tab_;
		private GitWrapper git_;

		public ViewModel(string git_repository_path, RepositoryTab repository_tab)
		{
			Debug.Assert(Util.IsValidGitDirectory(git_repository_path));

			Directory = git_repository_path;

			git_ = new GitWrapper(Directory, this);
			repository_tab_ = repository_tab;

			InitializePending();
			InitializeHistory();

			PushCommand = new DelegateCommand((object parameter) => Push());
			RefreshCommand = new DelegateCommand(async (object parameter) => {
				await Refresh();
			});
			ViewTimelapseCommand = new DelegateCommand((object parameter) => ViewTimeLapse());
			FetchAllCommand = new DelegateCommand((object parameter) => FetchAll());
			PullCommand = new DelegateCommand(Pull);
		}

		public void ViewTimeLapse()
		{
			if (string.IsNullOrEmpty(SelectedPath))
			{
				Service.GetInstance().ShowMsg("Select a file first");
				return;
			}
			git_.ViewTimeLapse(SelectedPath);
		}

		public void FetchAll()
		{
			DoWithProgressWindow("fetch --all");
		}

		public async void DoWithProgressWindow(string cmd)
		{
			// http://stackoverflow.com/questions/2796470/wpf-create-a-dialog-prompt
			var console_progress_window = new ConsoleProgressWindow(Directory, cmd);
			console_progress_window.Owner = Service.GetInstance().GetWindow();
			console_progress_window.ShowDialog();
			await Refresh();
		}

		public void Pull(object not_used)
		{
			DoWithProgressWindow("pull");
		}

		public void Push()
		{
			DoWithProgressWindow("push");
		}

		public async Task<bool> Refresh()
		{
			if (Util.IsValidGitDirectory(Directory) == false)
			{
				Service.GetInstance().ShowMsg(string.Format("{0} is a invalid root git repository", Directory));
				git_ = null;
				return false;
			}
			AddLog("Refreshing Directory:" + Directory);

			repository_tab_.EnterLoadingScreen();

			List<string> git_porcelain_result = await git_.GetGitStatusPorcelainAllAsync();
			RefreshPending(git_porcelain_result);
			RefreshHistory(null);
			RefreshBranch();
			RefreshSignature();
			repository_tab_.TreeView_Update(null, null);
			AddLog(git_porcelain_result);
			AddLog("Refreshed");

			repository_tab_.LeaveLoadingScreen();

			return true;
		}

		private void RefreshSignature()
		{
			DisplayAuthor = git_.GetSignature();
			NotifyPropertyChanged("DisplayAuthor");
		}

		private void RefreshBranch()
		{
			if (git_ == null)
			{
				Branch = "Unknown";
			}
			else
			{
				string output = git_.GetCurrentBranchName();
				string ahead_or_behind = git_.GetCurrentBranchTrackingRemote();
				if (ahead_or_behind.Length > 0)
				{
					output = string.Format("{0} - ({1})", git_.GetCurrentBranchName(), ahead_or_behind);
				}
				Branch = output;
			}
			NotifyPropertyChanged("Branch");
		}

		public void AddLog(string log)
		{
			if (string.IsNullOrEmpty(log))
			{
				return;
			}
			Log += String.Format("[{0}] {1}\n", DateTime.Now.ToLocalTime(), log);
			NotifyPropertyChanged("Log");
			repository_tab_.ScrollToEndLogTextBox();
		}

		public void AddLog(List<string> logs)
		{
			Log += string.Format("[{0}] {1}\n", DateTime.Now.ToLocalTime(), string.Join("\n", logs));
			NotifyPropertyChanged("Log");
			repository_tab_.ScrollToEndLogTextBox();
		}

		private void NotifyPropertyChanged(string name)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(name));
			}
		}
		public ICommand RefreshCommand { get; private set; }
		public ICommand ViewTimelapseCommand { get; private set; }
		public ICommand FetchAllCommand { get; private set; }
		public ICommand PullCommand { get; private set; }
		public ICommand PushCommand { get; private set; }

		public string Directory { get; set; }
		public string Log { get; set; }
		public string Branch { get; set; }
		public string DisplayAuthor { get; set; }
	}
}
