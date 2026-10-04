<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { auth, resetUserPasskeys } from '#lib/auth.svelte.ts';

	let email = $state('');
	let confirming = $state(false);
	let busy = $state(false);
	let error = $state<string | null>(null);
	let done = $state<string | null>(null);

	$effect(() => {
		if (!auth.me) {
			goto(resolve('/login'));
		}
	});

	function handleSubmit(event: SubmitEvent) {
		event.preventDefault();
		error = null;
		done = null;
		// 取り消せない操作のため、実行前に影響を示して確認を求める。
		confirming = true;
	}

	async function handleConfirm() {
		busy = true;
		error = null;
		try {
			await resetUserPasskeys(email);
			done = email;
			email = '';
			confirming = false;
		} catch (e) {
			error = e instanceof Error ? e.message : '再設定に失敗しました。';
		} finally {
			busy = false;
		}
	}
</script>

<div class="page">
	<h1>ユーザー管理</h1>

	{#if auth.me && !auth.isSystemAdmin}
		<p class="alert alert-danger">このページを表示する権限がありません。</p>
	{:else if auth.me}
		<form class="card stack" onsubmit={handleSubmit}>
			<h4>パスキーの再設定</h4>
			<p>
				パスキーをすべて失った利用者に、新しいパスキーを登録するためのリンクをメールで送ります。実行前に、依頼者が本人であることを確認してください。
			</p>
			<div class="field">
				<label for="email">利用者のメールアドレス</label>
				<input
					id="email"
					class="input"
					type="email"
					required
					disabled={confirming}
					bind:value={email}
				/>
			</div>

			{#if confirming}
				<div class="alert alert-danger stack">
					<p>
						{email} の登録済みパスキーをすべて削除し、サインイン中のセッションも無効にします。この操作は取り消せません。
					</p>
					<div class="row">
						<button class="btn btn-primary" type="button" disabled={busy} onclick={handleConfirm}>
							再設定する
						</button>
						<button class="btn" type="button" disabled={busy} onclick={() => (confirming = false)}>
							やめる
						</button>
					</div>
				</div>
			{:else}
				<div>
					<button class="btn btn-primary" type="submit">再設定の内容を確認</button>
				</div>
			{/if}

			{#if done}
				<p class="alert alert-success">{done} に再設定用のリンクを送信しました。</p>
			{/if}
			{#if error}
				<p class="alert alert-danger" role="alert">{error}</p>
			{/if}
		</form>
	{/if}
</div>
