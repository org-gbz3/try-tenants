<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { setupPasskey } from '#lib/auth.svelte.ts';
	import { describeWebAuthnError, isWebAuthnSupported } from '#lib/webauthn.ts';

	// 登録確認メールと、管理者による再設定メールの両方のリンクがこの画面を開く。
	const userId = page.url.searchParams.get('userId');
	const token = page.url.searchParams.get('token');
	const supported = isWebAuthnSupported();

	let name = $state('');
	let busy = $state(false);
	let error = $state<string | null>(null);

	async function handleSubmit(event: SubmitEvent) {
		event.preventDefault();
		if (!userId || !token) {
			return;
		}
		busy = true;
		error = null;
		try {
			await setupPasskey(userId, token, name);
			await goto(resolve('/account'));
		} catch (e) {
			error = describeWebAuthnError(e);
		} finally {
			busy = false;
		}
	}
</script>

<div class="page">
	<h1>パスキーの登録</h1>

	{#if !userId || !token}
		<p class="alert alert-danger">
			リンクが正しくありません。メールのリンクをもう一度開いてください。
		</p>
	{:else if !supported}
		<p class="alert alert-danger">このブラウザーはパスキーに対応していません。</p>
	{:else}
		<form class="card stack" onsubmit={handleSubmit}>
			<p>この端末でパスキーを作成します。端末の生体認証または PIN の入力を求められます。</p>
			<div class="field">
				<label for="name">パスキーの名前(任意)</label>
				<input
					id="name"
					class="input"
					maxlength="100"
					placeholder="例: 仕事用 PC"
					bind:value={name}
				/>
				<span class="caption">複数の端末で登録したときに見分けるための名前です。</span>
			</div>
			<div>
				<button class="btn btn-primary" type="submit" disabled={busy}>パスキーを作成</button>
			</div>
			{#if error}
				<p class="alert alert-danger" role="alert">{error}</p>
			{/if}
		</form>
	{/if}
</div>
