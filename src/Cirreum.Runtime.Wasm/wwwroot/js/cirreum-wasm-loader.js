/*!
 * cirreum-wasm-loader.js
 * Cirreum WebAssembly Loader Script
 * =====================================================================
 * Core bootstrap script for Cirreum-based Blazor WebAssembly apps.
 *
 * Responsibilities:
 *  • Read Cirreum configuration from #app cirreum-config JSON block
 *  • Resolve and load fingerprinted CSS/JS assets from the Import Map
 *  • Apply Subresource Integrity (SRI) where available
 *  • Initialize the selected Bootstrap color scheme (cirreum-bootstrap-*.css)
 *  • Persist the selected color scheme for future browser sessions
 *  • Load custom fonts, vendor libraries, and application resources
 *  • Handle authentication library loading (MSAL/OIDC)
 *  • Pre-authenticate before Blazor downloads when auth-mode="always"
 *  • Register PWA service workers when configured
 *  • Bootstrap the Blazor Error UI with Cirreum styling
 *  • Provide helper utilities for diagnostics and fallback loading
 *
 * Configuration:
 *  Place a JSON config block inside your #app div (cleared by Blazor on mount):
 *
 *  <div id="app">
 *    <script type="application/json" id="cirreum-config">
 *    {
 *      "app":   { "name": "My App", "assembly": "My.App.Wasm" },
 *      "theme": { "scheme": "default", "font": "default" },
 *      "auth":  { "type": "msal", "mode": "always",
 *                 "authority": "https://mytenant.ciamlogin.com/",
 *                 "clientId": "...", "redirectUri": "/authentication/login-callback" },
 *      "pwa":   { "script": "none" }
 *    }
 *    </script>
 *    <!-- splash screen -->
 *  </div>
 *
 * Legacy script tag attributes (fallback when no cirreum-config block present):
 *      asm-name     : Client assembly name
 *      app-name     : Friendly public name
 *      app-theme    : Bootstrap color scheme
 *                    (default | aqua | aspire | excel | office | outlook | windows)
 *      font-name    : Custom font name (from cdnfonts) or "default"
 *      auth-type    : Authentication mode (msal | oidc | dynamic | none)
 *      auth-type-url: URL template when auth-type is 'dynamic'
 *      auth-mode    : Pre-auth enforcement (always | optional)
 *      auth-authority: OIDC/MSAL authority URL
 *      auth-client-id: OAuth client ID
 *      auth-redirect-uri: OAuth redirect URI
 *      pwa-script   : Service worker file for PWA scenarios
 *
 * Cirreum's JavaScript API:
 *  • window.cirreum.app.getName()           - Get the application name
 *  • window.cirreum.app.getAssemblyName()   - Get the assembly name
 *  • window.cirreum.auth.getMode()          - Get declared auth mode (dynamic/msal/oidc/none)
 *  • window.cirreum.auth.getLibrary()       - Get resolved auth library (msal/oidc/none)
 *  • window.cirreum.auth.isEnabled()        - Check if auth is enabled
 *  • window.cirreum.tenant.getConfig()      - Get tenant auth configuration
 *  • window.cirreum.tenant.getSlug()        - Get tenant slug from URL
 *  • window.cirreum.tenant.getDisplayName() - Get tenant display name
 *  • window.cirreum.theme.getCurrent()      - Get current color scheme
 *  • window.cirreum.theme.set(scheme)       - Set and persist color scheme
 *  • window.cirreum.assets.loadCss(...)     - Load CSS with SRI support
 *  • window.cirreum.assets.loadJs(...)      - Load JS with SRI support
 *
 * Version:     @VERSION@
 * License:     MIT
 * Repository:  https://github.com/cirreum/Cirreum.Runtime.Wasm
 * Copyright:   2025 Cirreum Contributors
 * =====================================================================
 */

(function () {
	"use strict";

	// =====================================================================
	// Configuration & Constants
	// =====================================================================

	const validThemes = ["default", "aqua", "aspire", "excel", "office", "outlook", "windows", "none", "custom"];

	// -----------------------------------------------------------------------
	// Read cirreum-config JSON block from #app (preferred)
	// Falls back to script tag attributes for backwards compatibility
	// -----------------------------------------------------------------------

	let _config = null;

	function readCirreumConfig() {
		if (_config !== null) return _config;
		try {
			const el = document.getElementById("cirreum-config");
			if (el) {
				_config = JSON.parse(el.textContent);
				console.debug("[cirreum] Loaded configuration from #cirreum-config block.");
				return _config;
			}
		} catch {
			console.warn("[cirreum] Failed to parse #cirreum-config block — falling back to script tag attributes.");
		}
		_config = {};
		return _config;
	}

	function cfg(section, key) {
		return readCirreumConfig()?.[section]?.[key] ?? null;
	}

	// -----------------------------------------------------------------------
	// Script tag attributes (legacy / fallback)
	// -----------------------------------------------------------------------

	const currentScript = document.currentScript;

	// -----------------------------------------------------------------------
	// Resolved values — cirreum-config wins, script attributes are fallback
	// -----------------------------------------------------------------------

	const asmName = cfg("app", "assembly") ?? currentScript.getAttribute("asm-name");
	const appName = cfg("app", "name") ?? currentScript.getAttribute("app-name");
	const fontName = cfg("theme", "font") ?? currentScript.getAttribute("font-name");
	const pwaScript = cfg("pwa", "script") ?? currentScript.getAttribute("pwa-script");

	const authType = (cfg("auth", "type") ?? currentScript.getAttribute("auth-type") ?? "none").toLowerCase();
	const authTypeUrl = cfg("auth", "typeUrl") ?? currentScript.getAttribute("auth-type-url");
	const authMode = (cfg("auth", "mode") ?? currentScript.getAttribute("auth-mode") ?? "optional").toLowerCase();
	const authAuthority = cfg("auth", "authority") ?? currentScript.getAttribute("auth-authority");
	const authClientId = cfg("auth", "clientId") ?? currentScript.getAttribute("auth-client-id");
	const authRedirectUri = cfg("auth", "redirectUri")
		?? currentScript.getAttribute("auth-redirect-uri")
		?? (window.location.origin + "/authentication/login-callback");

	let appTheme = (cfg("theme", "scheme") ?? currentScript.getAttribute("app-theme")?.trim() ?? "default");

	// -----------------------------------------------------------------------
	// Validation
	// -----------------------------------------------------------------------

	if (!asmName) {
		console.error('[cirreum] Assembly name is required. Set "app.assembly" in #cirreum-config or the "asm-name" script attribute.');
		displayFatalError("Configuration Error", "Missing assembly name. Please contact support.");
		return;
	}

	let themeFromStorage = false;
	if (!validThemes.includes(appTheme)) {
		console.warn(`[cirreum] Invalid theme "${appTheme}". Falling back to "default".`);
		appTheme = "default";
	}


	// =====================================================================
	// Cirreum Namespace
	// =====================================================================

	window.cirreum = window.cirreum || {};

	// ---------------------------------------------------------------------
	// cirreum.app - Application metadata
	// ---------------------------------------------------------------------

	window.cirreum.app = {
		/**
		 * Gets the application name.
		 * @returns {string} The app name from config, or hostname as fallback.
		 */
		getName: function () {
			return appName || window.location.hostname.toUpperCase();
		},

		/**
		 * Gets the assembly name.
		 * @returns {string|null} The assembly name, or null if not set.
		 */
		getAssemblyName: function () {
			return asmName;
		}
	};

	// ---------------------------------------------------------------------
	// cirreum.auth - Authentication configuration
	// ---------------------------------------------------------------------

	window.cirreum.auth = {
		_mode: authType,
		_library: authType !== "dynamic" && authType !== "none" ? authType : null,

		/**
		 * Gets the declared auth mode from config.
		 * @returns {string} "dynamic", "msal", "oidc", or "none"
		 */
		getMode: function () {
			return this._mode;
		},

		/**
		 * Gets the resolved authentication library to use.
		 * For static modes this matches the mode. For dynamic it is resolved from tenant config.
		 * @returns {string} "msal", "oidc", or "none"
		 */
		getLibrary: function () {
			return this._library || "none";
		},

		/**
		 * Checks if authentication is enabled and a library will be loaded.
		 * @returns {boolean} True if auth library should be loaded.
		 */
		isEnabled: function () {
			return this._library === "msal" || this._library === "oidc";
		},

		/** @internal - called after dynamic config resolution */
		_setLibrary: function (library) {
			this._library = library;
		}
	};

	// ---------------------------------------------------------------------
	// cirreum.tenant - Tenant/multi-tenancy support
	// ---------------------------------------------------------------------

	window.cirreum.tenant = {
		_config: null,

		/**
		 * Gets the tenant authentication configuration.
		 * @returns {object|null} The tenant auth config, or null if not in dynamic mode.
		 */
		getConfig: function () {
			return this._config;
		},

		/**
		 * Gets the tenant slug from the URL path.
		 * Assumes URL pattern: https://host/{tenant}/...
		 * @returns {string} The tenant slug, or "default" if not found.
		 */
		getSlug: function () {
			const path = window.location.pathname;
			const segments = path.split("/").filter(s => s.length > 0);
			return segments.length > 0 ? segments[0] : "default";
		},

		/**
		 * Gets the tenant display name.
		 * @returns {string|null} The display name, or null if not available.
		 */
		getDisplayName: function () {
			return this._config?.displayName ?? null;
		},

		/** @internal */
		_setConfig: function (config) {
			this._config = config;
		}
	};

	// ---------------------------------------------------------------------
	// cirreum.theme - Color scheme management
	// ---------------------------------------------------------------------

	window.cirreum.theme = {
		/**
		 * Gets the current color scheme.
		 * @returns {string} The current theme name.
		 */
		getCurrent: function () {
			return appTheme;
		},

		/**
		 * Gets all valid theme names.
		 * @returns {string[]} Array of valid theme names.
		 */
		getValidThemes: function () {
			return [...validThemes];
		}
	};

	// ---------------------------------------------------------------------
	// cirreum.assets - Asset loading utilities
	// ---------------------------------------------------------------------

	window.cirreum.assets = {
		/**
		 * Loads a CSS file with optional SRI and fingerprint resolution.
		 * @param {string} href - The stylesheet URL.
		 * @param {string} [integrity] - Optional SRI hash.
		 * @param {string} [title] - Optional title for alternate stylesheets.
		 * @param {boolean} [disabled] - Whether to load as disabled.
		 */
		loadCss: function (href, integrity, title, disabled) {
			const linkCss = document.createElement("link");
			linkCss.crossOrigin = "anonymous";
			linkCss.referrerPolicy = "no-referrer";
			linkCss.rel = "stylesheet";
			linkCss.type = "text/css";

			const resolvedHref = resolveFingerprintedUrl(href);

			if (!integrity) {
				integrity = resolveIntegrityFor(resolvedHref);
			}

			if (integrity) linkCss.integrity = integrity;
			if (title) linkCss.title = title;
			if (disabled) linkCss.disabled = disabled;

			linkCss.onerror = () => console.error("[cirreum] Error loading stylesheet:", resolvedHref);
			linkCss.href = resolvedHref;
			document.head.appendChild(linkCss);
		},

		/**
		 * Loads a JavaScript file with optional SRI and fingerprint resolution.
		 * @param {string} src - The script URL.
		 * @param {string} [integrity] - Optional SRI hash.
		 */
		loadJs: function (src, integrity) {
			const scriptJs = document.createElement("script");
			scriptJs.crossOrigin = "anonymous";
			scriptJs.referrerPolicy = "no-referrer";
			scriptJs.async = false;
			scriptJs.defer = true;

			const resolvedSrc = resolveFingerprintedUrl(src);

			if (!integrity) {
				integrity = resolveIntegrityFor(resolvedSrc);
			}

			if (integrity) scriptJs.integrity = integrity;
			scriptJs.src = resolvedSrc;

			scriptJs.onerror = () => console.error("[cirreum] Error loading script:", resolvedSrc);
			document.body.appendChild(scriptJs);
		}
	};


	// =====================================================================
	// Internal Helper Functions
	// =====================================================================

	function resolveIntegrityFor(url) {
		const importMapScript = document.querySelector('script[type="importmap"]');
		if (!importMapScript) return null;

		try {
			const importMap = JSON.parse(importMapScript.textContent || importMapScript.innerHTML);
			if (importMap.integrity) {
				const lookupPath = "./" + url;
				if (importMap.integrity[lookupPath]) return importMap.integrity[lookupPath];
				if (importMap.integrity[url]) return importMap.integrity[url];
			}
		} catch (e) {
			console.warn("[cirreum] Failed to parse import map integrity for:", url, e);
		}
		return null;
	}

	function resolveFingerprintedUrl(originalPath) {
		const importMapScript = document.querySelector('script[type="importmap"]');
		if (importMapScript) {
			try {
				const importMap = JSON.parse(importMapScript.textContent || importMapScript.innerHTML);
				const lookupPath = "./" + originalPath;
				if (importMap.imports?.[lookupPath]) return importMap.imports[lookupPath].substring(2);
				if (importMap.imports?.[originalPath]) return importMap.imports[originalPath];
			} catch (e) {
				console.warn("[cirreum] Failed to resolve fingerprinted URL for:", originalPath, e);
			}
		}
		return originalPath;
	}

	function loadLocalCss(href) {
		const linkCss = document.createElement("link");
		linkCss.rel = "stylesheet";
		linkCss.type = "text/css";

		const resolvedHref = resolveFingerprintedUrl(href);
		const integrity = resolveIntegrityFor(resolvedHref);

		if (integrity) {
			linkCss.crossOrigin = "anonymous";
			linkCss.referrerPolicy = "no-referrer";
			linkCss.integrity = integrity;
		}

		linkCss.onerror = () => {
			console.error("[cirreum] Error loading stylesheet:", resolvedHref);
			if (resolvedHref !== href) {
				console.log("[cirreum] Attempting fallback to non-fingerprinted stylesheet:", href);
				const fallbackLink = document.createElement("link");
				fallbackLink.rel = "stylesheet";
				fallbackLink.type = "text/css";
				fallbackLink.href = href;
				document.head.appendChild(fallbackLink);
			}
		};

		linkCss.href = resolvedHref;
		document.head.appendChild(linkCss);
	}

	function loadLocalJs(src) {
		const scriptJs = document.createElement("script");

		const resolvedSrc = resolveFingerprintedUrl(src);
		const integrity = resolveIntegrityFor(resolvedSrc);

		if (integrity) {
			scriptJs.crossOrigin = "anonymous";
			scriptJs.referrerPolicy = "no-referrer";
			scriptJs.integrity = integrity;
		}

		scriptJs.src = resolvedSrc;
		scriptJs.onerror = () => {
			console.error("[cirreum] Error loading script:", resolvedSrc);
			if (resolvedSrc !== src) {
				console.log("[cirreum] Attempting fallback to non-fingerprinted version:", src);
				const fallbackScript = document.createElement("script");
				fallbackScript.src = src;
				fallbackScript.onerror = () => console.error("[cirreum] Fallback also failed for:", src);
				document.body.appendChild(fallbackScript);
			}
		};
		document.body.appendChild(scriptJs);
	}

	function buildBlazorErrorUI() {
		const createElementWithProps = (tag, props = {}) => {
			const element = document.createElement(tag);
			Object.assign(element, props);
			return element;
		};

		const icon = createElementWithProps("i", {
			className: "bi bi-exclamation-triangle-fill me-2",
			role: "presentation"
		});
		icon.setAttribute("aria-hidden", "true");

		const messageSpan = createElementWithProps("span", {
			innerHTML: "An unhandled error has occurred."
		});

		const reloadLink = createElementWithProps("a", {
			href: "#",
			className: "reload ms-3",
			innerHTML: "Reload"
		});

		const dismissButton = createElementWithProps("button", {
			id: "blazor-error-ui-dismiss",
			type: "button",
			className: "btn-close"
		});
		dismissButton.setAttribute("aria-label", "Dismiss error message");
		dismissButton.setAttribute("data-bs-dismiss", "alert");

		const errorUI = createElementWithProps("div", {
			id: "blazor-error-ui",
			className: "fs-6 alert alert-warning alert-dismissible fade show my-1 mx-3 fixed-bottom align-items-center",
			role: "alert"
		});
		errorUI.style.display = "none";
		errorUI.style.zIndex = "10001";

		[icon, messageSpan, reloadLink, dismissButton].forEach(el => errorUI.appendChild(el));
		document.body.appendChild(errorUI);

		const config = { attributes: true, childList: false, subtree: false };
		const observer = new MutationObserver((mutations) => {
			for (const mutation of mutations) {
				if (mutation.type === "attributes" && mutation.attributeName === "style") {
					observer.disconnect();
					mutation.target.style.display = "flex";
					observer.observe(errorUI, config);
				}
			}
		});

		dismissButton.addEventListener("click", () => {
			observer.disconnect();
			errorUI.style.display = "none";
			observer.observe(errorUI, config);
		});

		observer.observe(errorUI, config);
	}

	function displayAuthError(message) {
		displayFatalError("Authentication Error", message);
	}

	function displayFatalError(title, message) {
		const appContainer = document.getElementById("app");
		if (appContainer) {
			appContainer.innerHTML = `
			<div style="display: flex; justify-content: center; align-items: center; height: 100vh; font-family: system-ui, -apple-system, sans-serif;">
				<div style="text-align: center; padding: 2rem; max-width: 500px;">
					<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" fill="currentColor" viewBox="0 0 16 16" style="color: #dc3545; margin-bottom: 1rem;">
						<path d="M8.982 1.566a1.13 1.13 0 0 0-1.96 0L.165 13.233c-.457.778.091 1.767.98 1.767h13.713c.889 0 1.438-.99.98-1.767L8.982 1.566zM8 5c.535 0 .954.462.9.995l-.35 3.507a.552.552 0 0 1-1.1 0L7.1 5.995A.905.905 0 0 1 8 5zm.002 6a1 1 0 1 1 0 2 1 1 0 0 1 0-2z"/>
					</svg>
					<h2 style="margin: 0 0 0.5rem 0; color: #333;">${title}</h2>
					<p style="margin: 0; color: #666;">${message}</p>
				</div>
			</div>
		`;
		}
	}

	async function loadTenantAuthConfig() {
		if (!authTypeUrl) {
			console.error('[cirreum] auth.typeUrl is required when auth.type="dynamic"');
			displayAuthError("Authentication configuration error. Please contact support.");
			return null;
		}

		const tenant = window.cirreum.tenant.getSlug();
		const url = authTypeUrl.replace("{tenant}", encodeURIComponent(tenant));

		console.log(`[cirreum] Loading tenant auth config for "${tenant}" from ${url}`);

		try {
			const response = await fetch(url, {
				method: "GET",
				headers: { "Accept": "application/json" }
			});

			if (!response.ok) {
				console.error(`[cirreum] Failed to load tenant auth config: ${response.status} ${response.statusText}`);
				if (response.status === 404) {
					displayAuthError(`Tenant "${tenant}" not found. Please check the URL and try again.`);
				} else {
					displayAuthError(`Unable to load authentication configuration for "${tenant}". Please contact support.`);
				}
				return null;
			}

			const config = await response.json();

			if (!config.clientId) {
				console.error("[cirreum] Tenant auth config missing required field: clientId");
				displayAuthError("Invalid authentication configuration. Please contact support.");
				return null;
			}

			console.debug("[cirreum] Tenant auth config loaded:", {
				slug: config.slug,
				displayName: config.displayName,
				clientId: config.clientId?.substring(0, 8) + "...",
				isEntraExternal: config.isEntraExternal,
				hasAuthority: !!config.authority,
				hasTenantId: !!config.tenantId,
				hasDomain: !!config.domain
			});

			return config;

		} catch (error) {
			console.error("[cirreum] Error fetching tenant auth config:", error);
			displayAuthError("Unable to connect to authentication service. Please try again later.");
			return null;
		}
	}


	// =====================================================================
	// Pre-Auth Check (experimental)
	// Redirects to Entra before Blazor downloads when auth.mode="always"
	// and no valid MSAL session exists. Best-effort — any failure falls
	// through to normal Blazor boot.
	// =====================================================================

	async function preAuthCheck() {
		if (authMode !== "always" || authType === "none") {
			return false;
		}

		const clientId = authClientId ?? window.cirreum.tenant.getConfig()?.clientId;
		const authority = authAuthority ?? window.cirreum.tenant.getConfig()?.authority;

		if (!clientId || !authority) {
			console.warn("[cirreum] auth.mode=always requires auth.clientId and auth.authority. Skipping pre-auth check.");
			return false;
		}

		const params = new URLSearchParams(window.location.search);
		if (params.has("code") && params.has("state")) {
			console.debug("[cirreum] Auth callback detected — resuming boot.");
			return false;
		}

		if (hasValidMsalSession(clientId)) {
			console.debug("[cirreum] Valid MSAL session found — resuming boot.");
			return false;
		}

		console.debug("[cirreum] No valid session found. Redirecting to Entra before Blazor loads.");
		await redirectToEntra(clientId, authority);

		// redirectToEntra swallows its own errors and falls through on failure
		// so we can't know for certain navigation was triggered — but if we
		// reach here without throwing, assume redirect is in flight
		return true;
	}

	function hasValidMsalSession(clientId) {
		try {
			const prefix = `msal.${clientId}`;
			const now = Math.floor(Date.now() / 1000);

			return Object.keys(sessionStorage).some(key => {
				if (!key.startsWith(prefix)) return false;
				if (!key.includes("accesstoken") && !key.includes("idtoken")) return false;

				try {
					const entry = JSON.parse(sessionStorage.getItem(key));
					if (entry?.expiresOn) {
						return parseInt(entry.expiresOn, 10) > now;
					}
					// Can't determine expiry — assume valid, let MSAL decide
					return true;
				} catch {
					// Can't parse entry — assume valid, fall through to boot
					return true;
				}
			});
		} catch {
			// sessionStorage unavailable or enumeration failed — assume valid, fall through to boot
			return true;
		}
	}

	async function redirectToEntra(clientId, authority) {
		try {
			const verifier = generateRandomString(64);
			const challenge = await generateCodeChallenge(verifier);
			const state = generateRandomString(32);
			const nonce = generateRandomString(32);

			// Store verifier so MSAL can retrieve it on the callback
			sessionStorage.setItem(`msal.${clientId}.pkce.verifier`, verifier);
			sessionStorage.setItem(`msal.${clientId}.pkce.state`, state);

			const baseUrl = authority.replace(/\/$/, "");
			const authorizeUrl = `${baseUrl}/oauth2/v2.0/authorize`;

			const queryParams = new URLSearchParams({
				client_id: clientId,
				response_type: "code",
				redirect_uri: authRedirectUri,
				scope: "openid profile offline_access",
				response_mode: "query",
				state: state,
				nonce: nonce,
				code_challenge: challenge,
				code_challenge_method: "S256",
			});

			window.location.replace(`${authorizeUrl}?${queryParams.toString()}`);

		} catch (err) {
			// PKCE generation or redirect failed — fall through, Blazor handles auth normally
			console.warn("[cirreum] Pre-auth redirect failed, falling through to Blazor:", err);
		}
	}

	function generateRandomString(length) {
		const chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
		const array = new Uint8Array(length);
		crypto.getRandomValues(array);
		return Array.from(array, b => chars[b % chars.length]).join("");
	}

	async function generateCodeChallenge(verifier) {
		const encoder = new TextEncoder();
		const data = encoder.encode(verifier);
		const digest = await crypto.subtle.digest("SHA-256", data);
		return btoa(String.fromCharCode(...new Uint8Array(digest)))
			.replace(/\+/g, "-")
			.replace(/\//g, "_")
			.replace(/=/g, "");
	}


	// =====================================================================
	// Application Initialization
	// =====================================================================

	async function initializeApp() {

		//
		// Dynamic Auth: Load tenant config before anything else
		//
		if (authType === "dynamic") {
			console.debug("[cirreum] Dynamic auth mode — loading tenant configuration...");

			const config = await loadTenantAuthConfig();

			if (config) {
				window.cirreum.tenant._setConfig(config);

				const resolvedLibrary = config.authLibrary
					|| (config.tenantId || config.domain || config.isEntraExternal ? "msal" : "oidc");

				console.debug(`[cirreum] Resolved auth library: ${resolvedLibrary}`);
				window.cirreum.auth._setLibrary(resolvedLibrary);

			} else {
				console.error("[cirreum] Aborting app initialization due to auth config failure.");
				return;
			}
		}

		//
		// Pre-auth check: redirect before Blazor downloads if no valid session
		// Best-effort — any failure falls through to normal boot
		//
		const redirecting = await preAuthCheck();
		if (redirecting) return;

		//
		// HTML - Blazor Error UI
		//
		buildBlazorErrorUI();

		//
		// Custom Font
		//
		if (fontName && fontName.toLowerCase() !== "default") {
			window.cirreum.assets.loadCss(`https://fonts.cdnfonts.com/css/${fontName}`);
			document.documentElement.style.setProperty("--bs-font-sans-serif", `"${fontName}", sans-serif`);
		}

		//
		// Stylesheets
		//

		// Allow persisted scheme to override config
		try {
			const storedScheme = localStorage.getItem("user-color-scheme");
			if (storedScheme && validThemes.includes(storedScheme)) {
				appTheme = storedScheme;
				themeFromStorage = true;
			}
		} catch {
			// Ignore storage errors (private mode, etc.)
		}

		// Load appropriate Bootstrap CSS
		if (appTheme !== "none" && appTheme !== "custom") {
			if (!themeFromStorage) {
				try {
					localStorage.setItem("user-color-scheme", appTheme);
				} catch {
					// Ignore if storage is unavailable
				}
			}

			document.documentElement.setAttribute("data-color-scheme", appTheme);
			loadLocalCss(`_content/Cirreum.Components.WebAssembly/css/cirreum-bootstrap-${appTheme}.css`);
		} else {
			document.documentElement.removeAttribute("data-color-scheme");
			console.info(
				"[cirreum] Custom theme mode: No Bootstrap CSS loaded. " +
				"Please provide your own compiled Bootstrap CSS that includes Cirreum customizations."
			);
		}

		// cirreum-spinners.css
		loadLocalCss("_content/Cirreum.Components.WebAssembly/css/cirreum-spinners.css");

		// bootstrap-icons.min.css (v1.13.1)
		loadLocalCss("_content/Cirreum.Components.WebAssembly/css/bootstrap-icons.min.css");

		//
		// JavaScript
		//

		// Auth library
		if (cirreum.auth.isEnabled()) {
			const library = cirreum.auth.getLibrary();
			if (library === "msal") {
				loadLocalJs("_content/Microsoft.Authentication.WebAssembly.Msal/AuthenticationService.js");
			} else if (library === "oidc") {
				loadLocalJs("_content/Microsoft.AspNetCore.Components.WebAssembly.Authentication/AuthenticationService.js");
			}
		}

		// blazor.webassembly.js
		loadLocalJs("_framework/blazor.webassembly.js");

		// PWA service worker
		if ("serviceWorker" in navigator && pwaScript && pwaScript !== "none" && pwaScript.endsWith(".js")) {
			navigator.serviceWorker
				.register(pwaScript)
				.catch(err => console.warn("[cirreum] Service worker registration failed:", err));
		}

		// pace.js
		window.paceOptions = {
			ajax: {
				trackMethods: ["GET", "POST", "PUT", "PATCH"],
				ignoreURLs: ["browserLinkSignalR", "microsoftonline.com", "applicationinsights.azure.com", "graph.microsoft.com"]
			},
			eventLag: true,
			restartOnPushState: false
		};
		loadLocalJs("_content/Cirreum.Components.WebAssembly/js/pace.min.js");

		// popper.js (v2.11.8)
		loadLocalJs("_content/Cirreum.Components.WebAssembly/js/popper.min.js");

		// draggabilly.js (v3.0.0)
		loadLocalJs("_content/Cirreum.Components.WebAssembly/js/draggabilly.pkgd.min.js");

		//
		// App Styles
		//
		window.addEventListener("load", () => {
			loadLocalCss(`${asmName}.styles.css`);
		});
	}


	// =====================================================================
	// Bootstrap
	// =====================================================================

	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", () => initializeApp());
	} else {
		initializeApp();
	}

})();