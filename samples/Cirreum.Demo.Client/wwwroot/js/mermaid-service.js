let mermaidInstance = null;
let isInitialized = false;
let themeObserver = null;
let themeChangeCallback = null;

/**
 * Detects the current Bootstrap theme from the document.
 * Returns 'dark' or 'default' (Mermaid theme names).
 */
function detectBootstrapTheme() {
	const bsTheme = document.documentElement.getAttribute('data-bs-theme');
	return bsTheme === 'dark' ? 'dark' : 'default';
}

/**
 * Gets the effective theme to use for rendering.
 * @param {string|null} explicitTheme - Explicitly specified theme, or null to auto-detect
 * @returns {string} The Mermaid theme name to use
 */
function getEffectiveTheme(explicitTheme) {
	if (explicitTheme && explicitTheme !== 'auto') {
		return explicitTheme;
	}
	return detectBootstrapTheme();
}

export async function initializeMermaid() {
	if (isInitialized) return;

	try {
		// Load Mermaid from CDN
		if (!window.mermaid) {
			const script = document.createElement('script');
			script.src = 'https://unpkg.com/mermaid/dist/mermaid.min.js';
			script.async = true;

			await new Promise((resolve, reject) => {
				script.onload = resolve;
				script.onerror = reject;
				document.head.appendChild(script);
			});
		}

		// Initialize Mermaid with detected theme
		const initialTheme = detectBootstrapTheme();
		window.mermaid.initialize({
			startOnLoad: false,
			theme: initialTheme,
			securityLevel: 'loose',
			flowchart: {
				useMaxWidth: true,
				htmlLabels: true
			},
			sequence: {
				useMaxWidth: true
			},
			gantt: {
				useMaxWidth: true
			}
		});

		mermaidInstance = window.mermaid;
		isInitialized = true;
		console.log('Mermaid initialized successfully with theme:', initialTheme);

	} catch (error) {
		console.error('Failed to initialize Mermaid:', error);
		throw error;
	}
}

/**
 * Starts watching for Bootstrap theme changes.
 * @param {object} dotNetRef - .NET object reference for callback
 */
export function watchThemeChanges(dotNetRef) {
	if (themeObserver) {
		themeObserver.disconnect();
	}

	themeChangeCallback = dotNetRef;

	themeObserver = new MutationObserver((mutations) => {
		for (const mutation of mutations) {
			if (mutation.type === 'attributes' && mutation.attributeName === 'data-bs-theme') {
				const newTheme = detectBootstrapTheme();
				console.log('Bootstrap theme changed to:', newTheme);
				if (themeChangeCallback) {
					themeChangeCallback.invokeMethodAsync('OnThemeChanged', newTheme);
				}
			}
		}
	});

	themeObserver.observe(document.documentElement, {
		attributes: true,
		attributeFilter: ['data-bs-theme']
	});

	console.log('Theme change observer started');
}

/**
 * Stops watching for theme changes.
 */
export function stopWatchingThemeChanges() {
	if (themeObserver) {
		themeObserver.disconnect();
		themeObserver = null;
	}
	themeChangeCallback = null;
	console.log('Theme change observer stopped');
}

/**
 * Gets the current Bootstrap theme.
 * @returns {string} The current Mermaid theme name ('dark' or 'default')
 */
export function getCurrentTheme() {
	return detectBootstrapTheme();
}

/**
 * Renders a Mermaid diagram.
 * @param {string} diagramDefinition - The Mermaid diagram definition
 * @param {string|null} theme - Explicit theme, 'auto' for Bootstrap detection, or null for auto
 * @returns {Promise<string>} The rendered SVG
 */
export async function renderDiagram(diagramDefinition, theme = null) {
	if (!isInitialized || !mermaidInstance) {
		throw new Error('Mermaid not initialized');
	}

	try {
		// Generate unique ID for this diagram
		const diagramId = `mermaid-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;

		// Determine effective theme
		const effectiveTheme = getEffectiveTheme(theme);

		// Apply theme
		mermaidInstance.initialize({ theme: effectiveTheme });

		// Render the diagram and get the SVG
		const { svg } = await mermaidInstance.render(diagramId, diagramDefinition);

		return svg;

	} catch (error) {
		console.error('Failed to render Mermaid diagram:', error);
		throw error;
	}
}

export function clearDiagram(diagramId) {
	// Clean up any diagram-specific resources if needed
	const element = document.getElementById(diagramId);
	if (element) {
		element.innerHTML = '';
	}
}
