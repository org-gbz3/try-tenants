<script lang="ts">
	import { resolve } from '$app/paths';
	import { auth } from '#lib/auth.svelte.ts';
</script>

<div class="page">
	<h1>try-tenants</h1>

	{#if auth.me}
		<div class="stack">
			<p>{auth.me.email} でサインインしています。</p>
			<div class="row">
				<a class="btn btn-primary" href={resolve('/account')}>アカウント</a>
				{#if auth.isSystemAdmin}
					<a class="btn" href={resolve('/admin/users')}>ユーザー管理</a>
				{/if}
			</div>
		</div>
	{:else}
		<div class="stack">
			<p>パスワードの代わりに、端末の生体認証や PIN(パスキー)でサインインします。</p>
			<div class="row">
				<a class="btn btn-primary" href={resolve('/login')}>サインイン</a>
				<a class="btn" href={resolve('/register')}>新規登録</a>
			</div>
		</div>
	{/if}
</div>
