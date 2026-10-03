FROM debian:bookworm-slim

ARG MISE_VERSION=2026.10.0
# tzdata の設定ダイアログ(地域選択)を抑止する。ビルド時のみ有効で、実行時イメージには残らない。
ARG DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates curl git openssh-client sudo unzip xz-utils \
        locales tzdata \
        libc6 libgcc-s1 libgssapi-krb5-2 libicu72 libssl3 libstdc++6 zlib1g \
    && sed -i 's/^# *ja_JP.UTF-8 UTF-8/ja_JP.UTF-8 UTF-8/' /etc/locale.gen \
    && locale-gen ja_JP.UTF-8 \
    && ln -snf /usr/share/zoneinfo/Asia/Tokyo /etc/localtime \
    && echo Asia/Tokyo > /etc/timezone \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --shell /bin/bash --uid 1000 vscode \
    && echo 'vscode ALL=(ALL) NOPASSWD:ALL' > /etc/sudoers.d/vscode \
    && chmod 0440 /etc/sudoers.d/vscode

ENV LANG=ja_JP.UTF-8

RUN curl --fail --silent --show-error --location https://mise.run -o /tmp/install-mise.sh \
    && MISE_VERSION="${MISE_VERSION}" MISE_INSTALL_PATH=/usr/local/bin/mise sh /tmp/install-mise.sh \
    && rm /tmp/install-mise.sh \
    && mise --version

# mise のシム(実体は mise 本体)からは SDK の場所を辿れないため、C# 拡張機能(MSBuildLocator)向けに実体の場所を示す。
ENV DOTNET_ROOT="/home/vscode/.local/share/mise/dotnet-root"
# シムは mise.toml のあるディレクトリ配下でしかバージョンを解決できず、C# 拡張機能はワークスペース外から
# dotnet --info を実行するため失敗する。dotnet はシムより先に実体を見つけられるようにする。
ENV PATH="${DOTNET_ROOT}:/home/vscode/.local/share/mise/shims:${PATH}"
USER vscode
RUN echo 'eval "$(mise activate bash)"' >> /home/vscode/.bashrc
WORKDIR /workspaces/try-tenants
COPY --chown=vscode:vscode mise.toml /workspaces/try-tenants/mise.toml
RUN mise trust /workspaces/try-tenants/mise.toml \
    && mise install
