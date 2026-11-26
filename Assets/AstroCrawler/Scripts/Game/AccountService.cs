// Unity Player Accounts + Unity Authentication + Cloud Save. Local progress remains usable offline.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AstroCrawler
{
    public sealed class AccountService : MonoBehaviour
    {
        public const string CloudKey = "progress";
        private const string OwnerKey = "AstroCrawler.Account.v1.owner";
        private const string EmailKey = "AstroCrawler.Account.v1.email";
        private const string BaselineKey = "AstroCrawler.Account.v1.baseline";
        private const string BackupPrefix = "AstroCrawler.Account.v1.backup.";
        private const string DeletePendingKey = "AstroCrawler.Account.v1.deletePending";
        public bool DeletionPending => SignedIn && PlayerPrefs.GetString(DeletePendingKey, "") == AuthenticationService.Instance.PlayerId;
        private const float RetryInterval = 15f;
        public static AccountService Instance { get; private set; }
        public bool Ready { get; private set; }
        public bool CanSignIn { get; private set; }
        public bool Busy { get; private set; }
        public bool SignedIn => Ready && AuthenticationService.Instance.IsSignedIn;
        public bool CloudReady { get; private set; }
        public bool LoginPromptDismissed { get; private set; }
        public void DismissLoginPrompt() => LoginPromptDismissed = true;
        public string Email => PlayerPrefs.GetString(EmailKey, "");
        public string LastError { get; private set; }
        public event Action Changed;

        private readonly SemaphoreSlim cloudGate = new SemaphoreSlim(1, 1);
        private string lastUploaded;
        private string writeLock;
        private float nextRetry;
        private bool progressChanged;
        private bool handlingSignIn;
        private bool backgroundWork;
        private bool destroyed;
        private bool waitingForBrowser;
        private bool browserLeftApp;
        private float browserDeadline;
        private float browserReturnDeadline = -1f;

        public static AccountService EnsureCreated()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AccountService");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AccountService>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private async void Start() => await InitializeAsync();

        private async Task InitializeAsync()
        {
            Busy = true;
            try
            {
                await UnityServices.InitializeAsync();
                if (destroyed) return;
                Ready = true;
                try
                {
                    PlayerAccountService.Instance.SignedIn += OnPlayerAccountSignedIn;
                    PlayerAccountService.Instance.SignInFailed += OnPlayerAccountFailed;
                    Application.deepLinkActivated += OnLoginDeepLink;
                    CanSignIn = true;
                }
                catch (Exception exception) { Debug.LogWarning("[AstroCrawler/Account] Player Accounts: " + exception.Message); }
                if (SignedIn || HasCachedOwner()) await RestoreAsync();
            }
            catch (Exception exception) { Fail(exception); }
            finally { Busy = false; Notify(); }
        }

        private bool HasCachedOwner() => Ready && !string.IsNullOrEmpty(PlayerPrefs.GetString(OwnerKey, "")) &&
            AuthenticationService.Instance.SessionTokenExists;

        private async Task RestoreAsync()
        {
            if (!SignedIn || !AuthenticationService.Instance.IsAuthorized)
            {
                if (!HasCachedOwner()) return;
                if (SignedIn) AuthenticationService.Instance.SignOut(); // Keep the cached session token.
                await AuthenticationService.Instance.SignInAnonymouslyAsync(); // Restores this cached player, not a new guest.
            }
            if (!destroyed) await SyncAfterSignIn();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            destroyed = true;
            CloudReady = false;
            Instance = null;
            if (CanSignIn)
            {
                PlayerAccountService.Instance.SignedIn -= OnPlayerAccountSignedIn;
                PlayerAccountService.Instance.SignInFailed -= OnPlayerAccountFailed;
                Application.deepLinkActivated -= OnLoginDeepLink;
            }
        }

        public async void SignIn()
        {
            if (!CanSignIn || Busy || SignedIn) return;
            LastError = null;
            Busy = true;
            Notify();
            try
            {
                if (PlayerAccountService.Instance.IsSignedIn) OnPlayerAccountSignedIn();
                else
                {
                    waitingForBrowser = true;
                    browserLeftApp = false;
                    browserReturnDeadline = -1f;
                    browserDeadline = Time.realtimeSinceStartup + 180f;
                    await PlayerAccountService.Instance.StartSignInAsync();
                }
            }
            catch (Exception exception) { waitingForBrowser = false; Fail(exception); Busy = false; Notify(); }
        }

        private async void OnPlayerAccountSignedIn()
        {
            if (handlingSignIn || destroyed || !Busy) return;
            waitingForBrowser = false;
            handlingSignIn = true;
            Busy = true;
            CloudReady = false;
            Notify();
            try
            {
                await cloudGate.WaitAsync();
                try
                {
                    await AuthenticationService.Instance.SignInWithUnityAsync(PlayerAccountService.Instance.AccessToken);
                    if (destroyed) return;
                    var claims = PlayerAccountService.Instance.IdTokenClaims;
                    PlayerPrefs.SetString(EmailKey, claims?.Email ?? "");
                    PlayerPrefs.Save();
                }
                finally { cloudGate.Release(); }
                await SyncAfterSignIn();
            }
            catch (Exception exception) { Fail(exception); }
            finally { handlingSignIn = false; Busy = false; Notify(); }
        }

        private void OnPlayerAccountFailed(RequestFailedException exception)
        {
            waitingForBrowser = false;
            Fail(exception);
            Busy = false;
            Notify();
        }

        // Do not erase unsent progress or sign out if saving fails.
        public async void SignOut() => await SignOutAsync();

        private async Task SignOutAsync()
        {
            if (!SignedIn || Busy || DeletionPending) return;
            Busy = true;
            Notify();
            await cloudGate.WaitAsync();
            try
            {
                // Progress can still change while the network request is in flight.
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    await UploadLocked();
                    if (ProgressSnapshot.Capture(Config) == lastUploaded) break;
                }
                if (ProgressSnapshot.Capture(Config) != lastUploaded)
                    throw new InvalidOperationException("Progress changed during sign-out; it remains saved locally.");
                if (destroyed) return;
                AuthenticationService.Instance.SignOut(true);
                if (CanSignIn) PlayerAccountService.Instance.SignOut();
                CloudReady = false;
                LoginPromptDismissed = false;
                PlayerPrefs.DeleteKey(OwnerKey);
                PlayerPrefs.DeleteKey(EmailKey);
                PlayerPrefs.DeleteKey(BaselineKey);
                ProgressSnapshot.Wipe(Config);
                lastUploaded = null;
                writeLock = null;
                LastError = null;
                ReloadGame();
            }
            catch (Exception exception) { Fail(exception); }
            finally { cloudGate.Release(); Busy = false; Notify(); }
        }

        // Called only after explicit UI confirmation. Never upload between deletion stages.
        public async void DeleteGameAccount()
        {
            if (!SignedIn || Busy || Config == null) return;
            Busy = true;
            LastError = null;
            Notify();
            await cloudGate.WaitAsync();
            try
            {
                var playerId = AuthenticationService.Instance.PlayerId;
                RequirePlayer(playerId);
                CloudReady = false;
                PlayerPrefs.SetString(DeletePendingKey, playerId);
                PlayerPrefs.Save();
                await CloudSaveService.Instance.Data.Player.DeleteAllAsync(new Unity.Services.CloudSave.Models.Data.Player.DeleteAllOptions());
                RequirePlayer(playerId);
                await AuthenticationService.Instance.DeleteAccountAsync();
                if (SignedIn) throw new InvalidOperationException("Account deletion was not confirmed.");
                // This deletes the game's UGS identity, not the shared Unity Player Account.
                if (CanSignIn)
                {
                    try { PlayerAccountService.Instance.SignOut(); }
                    catch (Exception) { Debug.LogWarning("[AstroCrawler/Account] Provider sign-out unavailable after game account deletion."); }
                }
                ProgressSnapshot.Wipe(Config);
                foreach (var key in new[] { OwnerKey, EmailKey, BaselineKey, DeletePendingKey, BackupPrefix + playerId, BackupPrefix + "guest" })
                    PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                lastUploaded = null;
                writeLock = null;
                progressChanged = false;
                LoginPromptDismissed = true;
                ReloadGame();
            }
            catch (Exception exception) { Fail(exception); }
            finally { cloudGate.Release(); Busy = false; Notify(); }
        }

        private void RequirePlayer(string playerId)
        {
            if (destroyed || !SignedIn || !AuthenticationService.Instance.IsAuthorized ||
                AuthenticationService.Instance.PlayerId != playerId)
                throw new InvalidOperationException("Cloud save player session changed or expired.");
        }

        private async Task SyncAfterSignIn()
        {
            await cloudGate.WaitAsync();
            CloudReady = false;
            try
            {
                if (Config == null) throw new InvalidOperationException("Game configuration is not ready.");
                var playerId = AuthenticationService.Instance.PlayerId;
                RequirePlayer(playerId);
                if (DeletionPending) throw new InvalidOperationException("Account deletion pending; retry deletion in Settings.");
                var cloud = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { CloudKey });
                RequirePlayer(playerId);
                cloud.TryGetValue(CloudKey, out var item);
                var json = item?.Value.GetAs<string>();
                if (item != null && (!ProgressSnapshot.IsValid(Config, json) || string.IsNullOrEmpty(item.WriteLock)))
                    throw new InvalidOperationException("Cloud progress has an unsupported or invalid payload.");
                var local = ProgressSnapshot.Capture(Config);
                var owner = PlayerPrefs.GetString(OwnerKey, "");
                var baseline = PlayerPrefs.GetString(BaselineKey, "");
                var keepOfflineProgress = owner == playerId && !string.IsNullOrEmpty(baseline) &&
                    local != baseline && json == baseline;
                var reload = false;
                if (item != null)
                {
                    if (local != json && !keepOfflineProgress)
                    {
                        // Cloud wins on a genuine cross-device conflict. Retain the previous local snapshot per owner.
                        PlayerPrefs.SetString(BackupPrefix + (string.IsNullOrEmpty(owner) ? "guest" : owner), local);
                        if (!ProgressSnapshot.Apply(Config, json)) throw new InvalidOperationException("Cannot restore cloud progress.");
                        reload = true;
                    }
                    lastUploaded = json;
                    writeLock = item.WriteLock;
                }
                else
                {
                    if (!string.IsNullOrEmpty(owner) && owner != playerId)
                    {
                        PlayerPrefs.SetString(BackupPrefix + owner, local);
                        ProgressSnapshot.Wipe(Config);
                        reload = true;
                    }
                    lastUploaded = null;
                    writeLock = null;
                }
                PlayerPrefs.SetString(OwnerKey, playerId);
                PlayerPrefs.SetString(BaselineKey, lastUploaded ?? "");
                PlayerPrefs.Save();
                CloudReady = true;
                LastError = null;
                // Reload immediately after applying the account's save, even if the following upload fails.
                if (reload) ReloadGame();
                if (item == null || keepOfflineProgress) await UploadLocked();
            }
            catch { CloudReady = false; throw; }
            finally { cloudGate.Release(); }
        }

        // Public for explicit checkpoints; all background/sign-out writes use the same serialized path.
        public async Task SaveNowAsync()
        {
            PlayerPrefs.Save();
            await cloudGate.WaitAsync();
            try { await UploadLocked(); }
            finally { cloudGate.Release(); }
        }

        private async Task UploadLocked()
        {
            if (!CloudReady || DeletionPending || Config == null) throw new InvalidOperationException("Cloud progress has not finished loading.");
            var playerId = PlayerPrefs.GetString(OwnerKey, "");
            RequirePlayer(playerId);
            var json = ProgressSnapshot.Capture(Config);
            if (json == lastUploaded) return;
            try
            {
                var result = await CloudSaveService.Instance.Data.Player.SaveAsync(
                    new Dictionary<string, SaveItem> { { CloudKey, new SaveItem(json, writeLock) } });
                RequirePlayer(playerId);
                if (!result.TryGetValue(CloudKey, out var nextLock) || string.IsNullOrEmpty(nextLock))
                    throw new InvalidOperationException("Cloud Save did not confirm the write lock.");
                writeLock = nextLock;
                lastUploaded = json;
                PlayerPrefs.SetString(BaselineKey, json);
                PlayerPrefs.Save();
                LastError = null;
            }
            catch (CloudSaveConflictException) { CloudReady = false; throw; }
        }

        public void NotifyProgressChanged() => progressChanged = true;

        // Coalesce all mutations made in the same frame, including compound purchases/rewards.
        private async void LateUpdate()
        {
            TickBrowserLogin();
            if (!Ready || Busy || DeletionPending || backgroundWork || Time.unscaledTime < nextRetry || (!SignedIn && !HasCachedOwner())) return;
            if (CloudReady && AuthenticationService.Instance.IsAuthorized && !progressChanged) return;
            progressChanged = false;
            backgroundWork = true;
            try
            {
                if (!CloudReady || !AuthenticationService.Instance.IsAuthorized) await RestoreAsync();
                else await SaveNowAsync();
            }
            catch (Exception exception)
            {
                progressChanged = true;
                nextRetry = Time.unscaledTime + RetryInterval;
                Fail(exception);
            }
            finally { backgroundWork = false; Notify(); }
        }

        private async void OnApplicationPause(bool paused)
        {
            TrackBrowserFocus(!paused);
            if (!paused) return;
            PlayerPrefs.Save();
            if (!SignedIn || !CloudReady || Busy) return;
            try { await SaveNowAsync(); }
            catch (Exception exception) { Fail(exception); }
        }

        private void OnApplicationQuit() => PlayerPrefs.Save();
        private void OnApplicationFocus(bool focused) => TrackBrowserFocus(focused);

        private void TrackBrowserFocus(bool focused)
        {
            if (!waitingForBrowser) return;
            if (!focused) browserLeftApp = true;
            else if (browserLeftApp && browserReturnDeadline < 0f)
                browserReturnDeadline = Time.realtimeSinceStartup + 8f;
        }

        private void OnLoginDeepLink(string url)
        {
            if (!waitingForBrowser || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme != "unitydl" || uri.Host != "com.unityplayeraccounts." + Application.cloudProjectId) return;
            // Let the SDK exchange the authorization code; never log the callback URL or tokens.
            browserReturnDeadline = -1f;
            browserLeftApp = false;
            browserDeadline = Time.realtimeSinceStartup + 45f;
        }

        private void TickBrowserLogin()
        {
            if (!waitingForBrowser || handlingSignIn || destroyed) return;
            var now = Time.realtimeSinceStartup;
            if (now < browserDeadline && (browserReturnDeadline < 0f || now < browserReturnDeadline)) return;
            waitingForBrowser = false;
            // Cancel only the browser request, keeping Unity Authentication and all local/cloud progress.
            if (!PlayerAccountService.Instance.IsSignedIn) PlayerAccountService.Instance.SignOut();
            Busy = false;
            Fail(new TimeoutException("Browser sign-in was cancelled or did not return to the game. Please retry."));
            Notify();
        }
        private static GameConfig Config => GameManager.Instance != null ? GameManager.Instance.config : null;
        private static void ReloadGame() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        private void Fail(Exception exception) { if (destroyed) return; LastError = exception.Message; Debug.LogWarning("[AstroCrawler/Account] " + exception.Message); }
        private void Notify() { if (!destroyed) Changed?.Invoke(); }
    }
}
