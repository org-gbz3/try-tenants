// サーバー(ASP.NET Core Identity)とは WebAuthn の JSON 形式(バイナリは base64url)でやり取りする。
// PublicKeyCredential.parseCreationOptionsFromJSON などはブラウザーごとに対応状況が異なるため、変換を自前で行う。

type CredentialDescriptorJSON = Omit<PublicKeyCredentialDescriptor, 'id'> & { id: string };

export type CreationOptionsJSON = Omit<
	PublicKeyCredentialCreationOptions,
	'challenge' | 'user' | 'excludeCredentials'
> & {
	challenge: string;
	user: Omit<PublicKeyCredentialUserEntity, 'id'> & { id: string };
	excludeCredentials?: CredentialDescriptorJSON[];
};

export type RequestOptionsJSON = Omit<
	PublicKeyCredentialRequestOptions,
	'challenge' | 'allowCredentials'
> & {
	challenge: string;
	allowCredentials?: CredentialDescriptorJSON[];
};

export function base64UrlToBuffer(value: string): ArrayBuffer {
	const base64 = value.replace(/-/g, '+').replace(/_/g, '/');
	const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
	const binary = atob(padded);
	const bytes = new Uint8Array(binary.length);
	for (let i = 0; i < binary.length; i++) {
		bytes[i] = binary.charCodeAt(i);
	}
	return bytes.buffer;
}

export function bufferToBase64Url(buffer: ArrayBuffer | ArrayBufferView): string {
	const bytes =
		buffer instanceof ArrayBuffer
			? new Uint8Array(buffer)
			: new Uint8Array(buffer.buffer, buffer.byteOffset, buffer.byteLength);
	let binary = '';
	for (const byte of bytes) {
		binary += String.fromCharCode(byte);
	}
	return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function toDescriptor(descriptor: CredentialDescriptorJSON): PublicKeyCredentialDescriptor {
	return { ...descriptor, id: base64UrlToBuffer(descriptor.id) };
}

export function toCreationOptions(json: CreationOptionsJSON): PublicKeyCredentialCreationOptions {
	return {
		...json,
		challenge: base64UrlToBuffer(json.challenge),
		user: { ...json.user, id: base64UrlToBuffer(json.user.id) },
		excludeCredentials: json.excludeCredentials?.map(toDescriptor)
	};
}

export function toRequestOptions(json: RequestOptionsJSON): PublicKeyCredentialRequestOptions {
	return {
		...json,
		challenge: base64UrlToBuffer(json.challenge),
		allowCredentials: json.allowCredentials?.map(toDescriptor)
	};
}

function credentialBase(credential: PublicKeyCredential) {
	return {
		id: credential.id,
		rawId: bufferToBase64Url(credential.rawId),
		type: credential.type,
		authenticatorAttachment: credential.authenticatorAttachment,
		clientExtensionResults: credential.getClientExtensionResults()
	};
}

export function attestationToJSON(credential: PublicKeyCredential) {
	const response = credential.response as AuthenticatorAttestationResponse;
	return {
		...credentialBase(credential),
		response: {
			clientDataJSON: bufferToBase64Url(response.clientDataJSON),
			attestationObject: bufferToBase64Url(response.attestationObject),
			transports: response.getTransports?.() ?? []
		}
	};
}

export function assertionToJSON(credential: PublicKeyCredential) {
	const response = credential.response as AuthenticatorAssertionResponse;
	return {
		...credentialBase(credential),
		response: {
			clientDataJSON: bufferToBase64Url(response.clientDataJSON),
			authenticatorData: bufferToBase64Url(response.authenticatorData),
			signature: bufferToBase64Url(response.signature),
			userHandle: response.userHandle ? bufferToBase64Url(response.userHandle) : null
		}
	};
}

export function isWebAuthnSupported(): boolean {
	return typeof window !== 'undefined' && typeof window.PublicKeyCredential === 'function';
}

export async function createCredential(options: CreationOptionsJSON) {
	const credential = await navigator.credentials.create({ publicKey: toCreationOptions(options) });
	if (!(credential instanceof PublicKeyCredential)) {
		throw new Error('パスキーを作成できませんでした。');
	}
	return attestationToJSON(credential);
}

export async function getCredential(options: RequestOptionsJSON) {
	const credential = await navigator.credentials.get({ publicKey: toRequestOptions(options) });
	if (!(credential instanceof PublicKeyCredential)) {
		throw new Error('パスキーを取得できませんでした。');
	}
	return assertionToJSON(credential);
}

// ブラウザーの例外は英語で原因も分かりにくいため、利用者が次に取る行動が分かる文言に置き換える。
export function describeWebAuthnError(error: unknown): string {
	if (error instanceof DOMException) {
		switch (error.name) {
			case 'NotAllowedError':
				return 'パスキーの操作がキャンセルされたか、時間切れになりました。もう一度お試しください。';
			case 'InvalidStateError':
				return 'この認証器には既にパスキーが登録されています。';
			case 'SecurityError':
				return 'このアドレスではパスキーを使えません。https または localhost で開いてください。';
		}
	}
	return error instanceof Error ? error.message : '予期しないエラーが発生しました。';
}
