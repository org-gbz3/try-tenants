<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { signIn } from '#lib/auth.svelte.ts';
	import { describeWebAuthnError, isWebAuthnSupported } from '#lib/webauthn.ts';

	let busy = $state(false);
	let error = $state<string | null>(null);
	const supported = isWebAuthnSupported();

	async function handleSignIn() {
		busy = true;
		error = null;
		try {
			await signIn();
			await goto(resolve('/account'));
		} catch (e) {
			error = describeWebAuthnError(e);
		} finally {
			busy = false;
		}
	}
</script>

<div class="page">
	<h1>サインイン</h1>

	<section class="card stack">
		{#if supported}
			<p>この端末または同期されたパスキーを使ってサインインします。</p>
			<div>
				<button class="btn btn-primary" type="button" disabled={busy} onclick={handleSignIn}>
					パスキーでサインイン
				</button>
			</div>
		{:else}
			<p class="alert alert-danger">このブラウザーはパスキーに対応していません。</p>
		{/if}

		{#if error}
			<p class="alert alert-danger" role="alert">{error}</p>
		{/if}
	</section>

	<div class="stack">
		<p>アカウントをお持ちでない場合は <a href={resolve('/register')}>新規登録</a> してください。</p>
		<p class="muted">パスキーをすべて失った場合は、管理者にパスキーの再設定を依頼してください。</p>
	</div>
</div>
