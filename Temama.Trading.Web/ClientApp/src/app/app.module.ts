import { BrowserModule } from '@angular/platform-browser';
import { NgModule, ApplicationRef, ErrorHandler } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { RouterModule } from '@angular/router';
import { ModalModule } from 'ngx-bootstrap/modal';

import { ErrorMessageComponent } from './error-message/error-message.component';
import { ErrorModalComponent } from './error-message/error-message.component';
import { AppComponent } from './app.component';
import { NavMenuComponent } from './nav-menu/nav-menu.component';
import { StatsComponent } from './stats/stats.component';
import { BotsComponent } from './bots/bots.component';
import { ExchangesComponent } from './exchanges/exchanges.component';
import { NotifsComponent } from './notifs/notifs.component';
import { SettingsComponent } from './settings/settings.component';
import { AppLogComponent } from './app-log/app-log.component';
import { LoginComponent } from './login/login.component';
import { appRoutes } from './routes';
import { AuthGuard } from './login/auth.guard';
import { UserService } from './login/UserService';
import { AuthInterceptor } from './login/auth.interceptor';

@NgModule({
  declarations: [
    AppComponent,
    NavMenuComponent,
    StatsComponent,
    BotsComponent,
    ExchangesComponent,
    NotifsComponent,
    SettingsComponent,
    AppLogComponent,
    ErrorModalComponent,
    LoginComponent
  ],
  imports: [
    BrowserModule.withServerTransition({ appId: 'ng-cli-universal' }),
    HttpClientModule,
    FormsModule,
    RouterModule.forRoot(appRoutes),
    ModalModule.forRoot()
  ],
  providers: [
    {
      provide: ErrorHandler,
      useClass: ErrorMessageComponent
    },
    {
      provide: HTTP_INTERCEPTORS,
      useClass: AuthInterceptor,
      multi: true
    },
    UserService,
    AuthGuard
  ],
  entryComponents: [
    ErrorModalComponent
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
