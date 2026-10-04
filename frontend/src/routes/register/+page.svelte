<script lang="ts">
	import { register } from '#lib/auth.svelte.ts';

	let email = $state('');
	let busy = $state(false);
	let sent = $state(false);
	let error = $state<string | null>(null);

	async function handleSubmit(event: SubmitEvent) {
		event.preventDefault();
		busy = true;
		error = null;
		try {
			await register(email);
			sent = true;
		} catch (e) {
			error = e instanceof Error ? e.message : '登録に失敗しました。';
		} finally {
			busy = false;
		}
	}
</script>

<div class="page">
	<h1>新規登録</h1>

	{#if sent}
		<section class="card stack">
			<p class="alert alert-success">確認メールを送信しました。</p>
			<p>
				{email} 宛のメールに記載されたリンクを 24 時間以内に開き、パスキーを登録してください。
			</p>
			<p class="muted">
				既に登録済みのメールアドレスにはメールを送信しません。届かない場合は、迷惑メールフォルダーも確認してください。
			</p>
		</section>
	{:else}
		<form class="card stack" onsubmit={handleSubmit}>
			<p>メールアドレスの確認後、この端末でパスキーを作成します。パスワードは使いません。</p>
			<div class="field">
				<label for="email">メールアドレス</label>
				<input
					id="email"
					class="input"
					type="email"
					autocomplete="email"
					required
					maxlength="256"
					bind:value={email}
				/>
			</div>
			<div>
				<button class="btn btn-primary" type="submit" disabled={busy}>確認メールを送信</button>
			</div>
			{#if error}
				<p class="alert alert-danger" role="alert">{error}</p>
			{/if}
		</form>
	{/if}
</div>
