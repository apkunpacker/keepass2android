/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. 

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */


using System;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Content.PM;
using KeePassLib.Utility;

namespace keepass2android
{
    [Activity(Label = "@string/kp2a_findUrl", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden, Theme = "@style/MyTheme_ActionBar")]
#if NoNet
    [MetaData("android.app.searchable", Resource = "@xml/searchable_offline")]
#else
#if DEBUG
	[MetaData("android.app.searchable", Resource = "@xml/searchable_debug")]
#else
    [MetaData("android.app.searchable", Resource = "@xml/searchable")]
#endif
#endif
	[MetaData("android.app.default_searchable", Value = "keepass2android.search.SearchResults")]
	[IntentFilter(new[] { Intent.ActionSearch }, Categories = new[] { Intent.CategoryDefault })]
	public class ShareUrlResults : GroupBaseActivity
	{

		public ShareUrlResults (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

		public ShareUrlResults()
		{
		}

		public static void Launch(Activity act, SearchUrlTask task)
		{
			Intent i = new Intent(act, typeof(ShareUrlResults));
			task.ToIntent(i);
			act.StartActivityForResult(i, 0);
		}


		private Database _db;
        

        public override bool IsSearchResult
        {
            get { return true; }
        }

        protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			//if user presses back to leave this activity:
			SetResult(Result.Canceled);

			_db = App.Kp2a.GetDb();
			if (App.Kp2a.DatabaseIsUnlocked)
			{
				String searchUrl = ((SearchUrlTask)AppTask).UrlToSearchFor;
				Query(searchUrl);	
			}
			// else: LockCloseListActivity.OnResume will trigger a broadcast (LockDatabase) which will cause the activity to be finished.
			
		}

		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			AppTask.ToBundle(outState);
		}

		private void Query(String url)
		{	
			try
			{
				//first: search for exact url
				Group = _db.SearchForExactUrl(url);
				if (!url.StartsWith("androidapp://"))
				{
					//if no results, search for host (e.g. "accounts.google.com")
					if (!Group.Entries.Any())
						Group = _db.SearchForHost(url, false);
					//if still no results, search for host, allowing subdomains ("www.google.com" in entry is ok for "accounts.google.com" in search (but not the other way around)
					if (!Group.Entries.Any())
						Group = _db.SearchForHost(url, true);
					
				}
				//if no results returned up to now, try to search through other fields as well:
				if (!Group.Entries.Any())
					Group = _db.SearchForText(url);
				//search for host as text
				if (!Group.Entries.Any())
					Group = _db.SearchForText(UrlUtil.GetHost(url.Trim()));

			} catch (Exception e)
			{
				Toast.MakeText(this, e.Message, ToastLength.Long).Show();
				SetResult(Result.Canceled);
				Finish();
				return;
			}
			
			//if there is exactly one match: open the entry
			if (Group.Entries.Count() == 1)
			{
				LaunchActivityForEntry(Group.Entries.Single(),0);
				return;
			}

			//show results:
			if (Group == null || (!Group.Entries.Any()))
			{
				SetContentView(Resource.Layout.searchurlresults_empty);
			} 
			
			SetGroupTitle();

            FragmentManager.FindFragmentById<GroupListFragment>(Resource.Id.list_fragment).ListAdapter = new PwGroupListAdapter(this, Group);

			View selectOtherEntry = FindViewById (Resource.Id.select_other_entry);

		    var newTask = new SelectEntryForUrlTask(url);
		    if (AppTask is SelectEntryTask currentSelectTask)
		        newTask.ShowUserNotifications = currentSelectTask.ShowUserNotifications;
            
            selectOtherEntry.Click += (sender, e) => {
				GroupActivity.Launch (this, newTask);
			};

			
			View createUrlEntry = FindViewById (Resource.Id.add_url_entry);

			if (App.Kp2a.GetDb().CanWrite)
			{
				createUrlEntry.Visibility = ViewStates.Visible;
				createUrlEntry.Click += (sender, e) =>
				{
					GroupActivity.Launch(this, new CreateEntryThenCloseTask { Url = url });
					Toast.MakeText(this, GetString(Resource.String.select_group_then_add, new Java.Lang.Object[] { GetString(Resource.String.add_entry) }), ToastLength.Long).Show();
				};
			}
			else
			{
				createUrlEntry.Visibility = ViewStates.Gone;
			}

			Util.MoveBottomBarButtons(Resource.Id.select_other_entry, Resource.Id.add_url_entry, Resource.Id.bottom_bar, this);
		}

		public override bool OnSearchRequested()
		{
			Intent i = new Intent(this, typeof(SearchActivity));
			AppTask.ToIntent(i);
			i.SetFlags(ActivityFlags.ForwardResult);
			StartActivity(i);
			return true;
		}

		public override bool BottomBarAlwaysVisible
		{
			get { return true; }
		}

	    protected override int ContentResourceId
	    {
			get { return Resource.Layout.searchurlresults; }
	    }
	}}

