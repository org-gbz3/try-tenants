import {
	createCredential,
	getCredential,
	type CreationOptionsJSON,
	type RequestOptionsJSON
} from './webauthn.ts';

// 認証情報は HttpOnly Cookie にだけ持たせ、localStorage などには保存しない(AGENTS.md の方針)。

export class ApiError extends Error {
	constructor(
		readonly status: number,
		message: string
	) {
		super(message);
	}
}

/** 401: 未認証。サインイン画面へ誘導する。 */
export class UnauthorizedError extends ApiError {}

/** 403: サインイン済みだが権限が無い。 */
export class ForbiddenError extends ApiError {}

export type Me = { email: string; roles: string[] };

export type Passkey = { id: string; name: string | null; createdAt: string; isBackedUp: boolean };

class AuthState {
	me = $state<Me | null>(null);
	loaded = $state(false);
	isSystemAdmin = $derived(this.me?.roles.includes('SystemAdmin') ?? false);
}

export const auth = new AuthState();

const safeMethods = new Set(['GET', 'HEAD', 'OPTIONS']);

async function errorMessage(response: Response, fallback: string): Promise<string> {
	if (response.status === 429) {
		return '操作が集中しています。しばらく待ってから再度お試しください。';
	}
	try {
		const problem = await response.json();
		return typeof problem?.detail === 'string' ? problem.detail : fallback;
	} catch {
		return fallback;
	}
}

async function fetchCsrfToken(): Promise<string> {
	const response = await fetch('/api/auth/csrf', { credentials: 'same-origin' });
	if (!response.ok) {
		throw new ApiError(response.status, '通信に失敗しました。');
	}
	const body: { token: string } = await response.json();
	return body.token;
}

/**
 * API 呼び出しの共通口。更新系のメソッドでは直前に CSRF トークンを取得して付与する。
 * トークンはユーザーに紐づき、ログイン前後で無効になるため、使い回さずに毎回取得する。
 */
export async function apiFetch(path: string, init: RequestInit = {}): Promise<Response> {
	const method = (init.method ?? 'GET').toUpperCase();
	const headers = new Headers(init.headers);
	if (init.body !== undefined && !headers.has('Content-Type')) {
		headers.set('Content-Type', 'application/json');
	}
	if (!safeMethods.has(method)) {
		headers.set('X-CSRF-TOKEN', await fetchCsrfToken());
	}

	const response = await fetch(path, { ...init, method, headers, credentials: 'same-origin' });
	if (response.status === 401) {
		throw new UnauthorizedError(401, await errorMessage(response, 'サインインが必要です。'));
	}
	if (response.status === 403) {
		throw new ForbiddenError(403, await errorMessage(response, 'この操作を行う権限がありません。'));
	}
	if (!response.ok) {
		throw new ApiError(response.status, await errorMessage(response, '処理に失敗しました。'));
	}
	return response;
}

function post(path: string, body?: unknown): Promise<Response> {
	return apiFetch(path, {
		method: 'POST',
		body: body === undefined ? undefined : JSON.stringify(body)
	});
}

export async function loadMe(): Promise<void> {
	try {
		const response = await apiFetch('/api/auth/me');
		auth.me = await response.json();
	} catch (error) {
		if (!(error instanceof UnauthorizedError)) {
			throw error;
		}
		auth.me = null;
	} finally {
		auth.loaded = true;
	}
}

export async function register(email: string): Promise<void> {
	await post('/api/auth/register', { email });
}

export async function setupPasskey(userId: string, token: string, name: string): Promise<void> {
	const options: CreationOptionsJSON = await (
		await post('/api/auth/passkey-setup/options', { userId, token })
	).json();
	const credential = await createCredential(options);
	await post('/api/auth/passkey-setup', { userId, token, credential, name });
	await loadMe();
}

export async function signIn(): Promise<void> {
	const options: RequestOptionsJSON = await (await post('/api/auth/login/options')).json();
	const credential = await getCredential(options);
	await post('/api/auth/login', { credential });
	await loadMe();
}

export async function signOut(): Promise<void> {
	await post('/api/auth/logout');
	auth.me = null;
}

export async function listPasskeys(): Promise<Passkey[]> {
	return (await apiFetch('/api/auth/passkeys')).json();
}

export async function addPasskey(name: string): Promise<void> {
	const options: CreationOptionsJSON = await (await post('/api/auth/passkeys/options')).json();
	const credential = await createCredential(options);
	await post('/api/auth/passkeys', { credential, name });
}

export async function deletePasskey(id: string): Promise<void> {
	await apiFetch(`/api/auth/passkeys/${encodeURIComponent(id)}`, { method: 'DELETE' });
}

export async function resetUserPasskeys(email: string): Promise<void> {
	await post('/api/admin/users/passkey-reset', { email });
}
