import { describe, expect, it } from 'vitest';
import {
	base64UrlToBuffer,
	bufferToBase64Url,
	toCreationOptions,
	toRequestOptions,
	type CreationOptionsJSON
} from './webauthn.ts';

describe('base64url', () => {
	it('パディング無しの base64url とバイト列を相互に変換できる', () => {
		const bytes = new Uint8Array([0xfb, 0xff, 0x00, 0x01, 0x7e]);

		const encoded = bufferToBase64Url(bytes);

		expect(encoded).toBe('-_8AAX4');
		expect(new Uint8Array(base64UrlToBuffer(encoded))).toEqual(bytes);
	});

	it('TypedArray の一部を指すビューは、その範囲だけを変換する', () => {
		const bytes = new Uint8Array([1, 2, 3, 4]);

		expect(bufferToBase64Url(bytes.subarray(1, 3))).toBe(bufferToBase64Url(new Uint8Array([2, 3])));
	});
});

describe('options の変換', () => {
	it('作成オプションのチャレンジ・ユーザーID・除外資格情報をバイト列にする', () => {
		const json: CreationOptionsJSON = {
			rp: { id: 'localhost', name: 'localhost' },
			user: { id: 'dXNlcg', name: 'a@example.com', displayName: 'a@example.com' },
			challenge: 'AQID',
			pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
			excludeCredentials: [{ type: 'public-key', id: 'BAU' }]
		};

		const options = toCreationOptions(json);

		expect(new Uint8Array(options.challenge as ArrayBuffer)).toEqual(new Uint8Array([1, 2, 3]));
		expect(new TextDecoder().decode(options.user.id as ArrayBuffer)).toBe('user');
		expect(new Uint8Array(options.excludeCredentials![0].id as ArrayBuffer)).toEqual(
			new Uint8Array([4, 5])
		);
		expect(options.pubKeyCredParams).toEqual(json.pubKeyCredParams);
	});

	it('要求オプションのチャレンジをバイト列にし、許可資格情報が無い場合はそのままにする', () => {
		const options = toRequestOptions({ challenge: 'AQID', rpId: 'localhost' });

		expect(new Uint8Array(options.challenge as ArrayBuffer)).toEqual(new Uint8Array([1, 2, 3]));
		expect(options.allowCredentials).toBeUndefined();
		expect(options.rpId).toBe('localhost');
	});
});
