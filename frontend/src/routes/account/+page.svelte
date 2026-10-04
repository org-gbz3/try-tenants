<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import {
		addPasskey,
		auth,
		deletePasskey,
		listPasskeys,
		signOut,
		UnauthorizedError,
		type Passkey
	} from '#lib/auth.svelte.ts';
	import { describeWebAuthnError } from '#lib/webauthn.ts';

	let passkeys = $state<Passkey[]>([]);
	let newName = $state('');
	let busy = $state(false);
	let error = $state<string | null>(null);

	const dateFormat = new Intl.DateTimeFormat('ja-JP', { dateStyle: 'medium', timeStyle: 'short' });

	$effect(() => {
		if (!auth.me) {
			goto(resolve('/login'));
			return;
		}
		refresh();
	});

	async function run(action: () => Promise<void>) {
		busy = true;
		error = null;
		try {
			await action();
		} catch (e) {
			if (e instanceof UnauthorizedError) {
				auth.me = null;
				return;
			}
			error = describeWebAuthnError(e);
		} finally {
			busy = false;
		}
	}

	function refresh() {
		return run(async () => {
			passkeys = await listPasskeys();
		});
	}

	function handleAdd(event: SubmitEvent) {
		event.preventDefault();
		return run(async () => {
			await addPasskey(newName);
			newName = '';
			passkeys = await listPasskeys();
		});
	}

	function handleDelete(passkey: Passkey) {
		return run(async () => {
			await deletePasskey(passkey.id);
			passkeys = await listPasskeys();
		});
	}

	function handleSignOut() {
		return run(async () => {
			await signOut();
			await goto(resolve('/'));
		});
	}
</script>

{#if auth.me}
	<div class="page">
		<h1>アカウント</h1>

		<section class="stack">
			<h4>サインイン中のアカウント</h4>
			<div class="row">
				<span>{auth.me.email}</span>
				<button class="btn" type="button" disabled={busy} onclick={handleSignOut}
					>サインアウト</button
				>
			</div>
		</section>

		{#if error}
			<p class="alert alert-danger" role="alert">{error}</p>
		{/if}

		<section class="stack">
			<h4>パスキー</h4>
			<ul class="list">
				{#each passkeys as passkey (passkey.id)}
					<li class="row passkey">
						<div class="stack passkey-info">
							<span>{passkey.name ?? '名前なし'}</span>
							<span class="caption">
								{dateFormat.format(new Date(passkey.createdAt))} に登録
								{#if passkey.isBackedUp}<span class="badge">同期済み</span>{/if}
							</span>
						</div>
						<button
							class="btn btn-danger"
							type="button"
							disabled={busy || passkeys.length <= 1}
							onclick={() => handleDelete(passkey)}
						>
							削除
						</button>
					</li>
				{/each}
			</ul>
			{#if passkeys.length <= 1}
				<p class="caption">
					最後のパスキーは削除できません。端末の紛失に備えて、別の端末でもパスキーを追加しておくことをおすすめします。
				</p>
			{/if}
		</section>

		<form class="card stack" onsubmit={handleAdd}>
			<h4>パスキーを追加</h4>
			<div class="field">
				<label for="name">パスキーの名前(任意)</label>
				<input
					id="name"
					class="input"
					maxlength="100"
					placeholder="例: スマートフォン"
					bind:value={newName}
				/>
			</div>
			<div>
				<button class="btn btn-primary" type="submit" disabled={busy}
					>この端末でパスキーを追加</button
				>
			</div>
		</form>
	</div>
{/if}

<style>
	.passkey {
		justify-content: space-between;
	}

	.passkey-info {
		gap: var(--space-2xs);
	}
</style>
